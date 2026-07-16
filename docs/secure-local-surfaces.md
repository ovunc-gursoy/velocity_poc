# Plan: securing the CLI and Local MCP surfaces

> Status: proposal, not yet built. Written 2026-07-16.
> Answers "secure the CLI and Local MCP the same way we secured the Remote MCP" — but the honest
> answer reshapes the question, so read the reasoning before the steps.

## TL;DR

The remote server authenticates because it is a **shared network service** — it must vouch for
strangers it has never met. The CLI and Local MCP are not that. They run on the user's own
machine, as that user, launched by that user. **There is no stranger to authenticate.**

Copying the remote's OAuth onto them as-is secures nothing: anyone who can run the binary already
controls the machine it runs on. And gating tools per-user locally (currency vs World Cup) is
**theater** as long as the data is public (Frankfurter) and local (SQLite) — a denied user can
fork the code, read the SQLite, or call Frankfurter directly.

Securing them *meaningfully* means one thing: **move authorization to the resource, not the
surface.** The moment a tool touches a private resource, the local surface authenticates the user
and carries a short-lived token; the *backend* enforces who may do what. Same Clerk, same tokens,
same allowlist — enforced where it cannot be bypassed. Until a private resource exists, the
correct action is to do nothing and say so.

## Why the remote model does not transplant

| | Remote MCP | CLI / Local MCP |
|---|---|---|
| Who calls it | Anyone on the network | The user who launched it |
| Trust established by | OAuth — proves identity to a service that can't see the caller | The OS — the user already authenticated to their own machine |
| What auth buys | Real: keeps out everyone not signed in | Nothing against the launching user; they own the process |
| Per-user tool gate | Enforceable — the server holds the resource | Bypassable — the user holds the code and the data is public |

The architecture diagram already says this: *"Skill, Local MCP and CLI run as the user, with the
user's own credentials."* The trust boundary is drawn around the Remote MCP alone, on purpose.

## The one real hole this is pointing at

The legitimate worry underneath the request: **if the remote gates the currency tool but the CLI
does not, isn't the CLI a bypass of that gate?**

Today: no — because the "resource" the currency tool reaches is *public* (Frankfurter). There is
nothing to bypass; a denied user could hit Frankfurter with `curl`. The gate only ever protected
a decision, not a secret.

It becomes a *real* hole the instant a tool reaches a **private** resource (the diagram's Custom
APIs / DBs / Services). Then a locally-run tool that isn't authenticated is an unauthenticated
path to private data. The fix is not "add auth to the CLI process" — it is "make the private
resource require a user token, and have every surface carry one." Enforcement belongs at the
resource, which is the only place all four surfaces converge and the only place the user cannot
edit away.

## Target model

```
  CLI / Local MCP ──(user's short-lived token, via PKCE)──► Backend API ──► private resource
                                                             │
                                                             └─ validates token (Clerk JWKS)
                                                                enforces the SAME allowlist/policy
                                                                as the Remote MCP
```

- The local surface is an **OAuth public client**: Authorization Code + PKCE with a loopback
  redirect (RFC 8252 — the standard for native/CLI apps; it's how `gh` and Claude Code log in).
  This Clerk instance supports it (`grant_types: authorization_code`, `token_endpoint_auth_methods: none`,
  `code_challenge_methods: S256`). **Device flow is not available** here — the AS advertises no
  `device_code` grant — so loopback PKCE is the path.
- The token lives in the **OS credential store** (Windows Credential Manager / macOS Keychain /
  libsecret), never plaintext on disk, never in the repo.
- The local process holds **no long-lived secret** and enforces **nothing** itself. All it does is
  obtain and attach a user token. The backend does the validating and authorizing — identical code
  to what the Remote MCP already runs.

## The required sequence: resource API first

The steps below are ordered, not a menu. The order is forced by one fact:

**A check that runs on the user's machine is a check the user can delete.** Today all four surfaces
share the same *code* (core), but core runs **in-process** — the CLI links it, the Local MCP hosts
it, both under the user's control. An `[Authorize]` check evaluated inside core is one the user can
patch out, and the data it guards (public Frankfurter, local SQLite) is reachable without it
anyway. So the shared thing cannot be shared *code*. It must be a shared **network resource** that
holds the private data and does the enforcing itself — the one boundary a local user cannot edit
around. That is the diagram's "Custom APIs" box, and it is why nothing else can come first.

Hence:

0. **Close the audience gap** (`docs/decisions.md`, 2026-07-16). The resource API will authorize
   based on these Clerk tokens; while a token minted for another resource on the same instance is
   still accepted, the API cannot safely trust one for anything private. This is the floor
   everything else stands on — see the inherited caveat below.
1. **Stand up the resource API** and move the private data behind it. Until this exists there is no
   lock, only a door.
2. **Enforce authorization at that API** — one policy, evaluated at the resource, identical for
   every surface. This is the step that makes per-user access real instead of theater, because it
   is the step the user cannot bypass.
3. **Then wire the authentication flows.** Each surface's job shrinks to "obtain a user token and
   carry it." Doing this *before* step 1 authenticates the user against nothing — a token proving
   identity to a door with no lock.

The phases below implement this sequence.

## Phased steps

**Phase A — a resource worth protecting.** Stand up the backend API layer the diagram draws and we
deliberately skipped (see the "Custom APIs" ADR). Until a private resource exists, everything below
is hypothetical — do not build auth for public data.

**Phase B — shared enforcement.** Extract the Remote MCP's token validation + allowlist policy into
a piece the backend API also uses, so "who may use the currency tool" is defined once and enforced
by whatever fronts the private resource.

**Phase C — CLI/Local as OAuth clients.**
- `velocity login` — runs Authorization Code + PKCE against Clerk via a loopback redirect, caches
  the token in the OS keychain. `velocity logout` clears it.
- Tools attach the cached token to backend calls; refresh via `refresh_token` when it expires.
- Local MCP does the same and passes the token on its outbound backend calls. (Its own stdio
  channel stays unauthenticated — that's the launching user, correctly trusted.)

**Phase D — hardening (mostly ties to CI, phase 6).**
- Sign the NuGet package / `dotnet tool` so installs are verifiable (supply-chain integrity).
- Audit: the backend logs who called what — the only place a local tool's use can be recorded.
- Secret hygiene: no secret in the tool; token in the keychain; short TTLs.

## Non-goals (do NOT do these)

- **Do not OAuth-gate the local tools against their own public data.** A login wall on a local
  World-Cup/FX utility secures nothing and annoys the user. Theater.
- **Do not embed a Clerk secret key or API key in the CLI/tool.** A distributed binary cannot keep
  a secret; assume anything shipped in it is public.
- **Do not try to stop a user from using a capability on their own machine when the resource is
  public.** You can't, and pretending to is worse than being honest about the boundary.

## Inherited caveat

The audience gap from the Remote MCP (`docs/decisions.md`, 2026-07-16) applies here too: these are
the same Clerk tokens with no `aud` claim. The backend that enforces authorization needs audience
binding closed before it trusts a token for anything private — otherwise a token minted for another
resource on the same Clerk instance is accepted. Closing that is a prerequisite for Phase A, not an
afterthought.

## Verification (when built)

- A non-allowlisted but authenticated user calling the private tool via CLI **and** Local MCP
  **and** Remote MCP is denied by the backend — identically, at the resource, on all three.
- The token exists only in the OS keychain: grep the disk and the repo for it → absent.
- The tool works with no ambient secret in env or repo.
