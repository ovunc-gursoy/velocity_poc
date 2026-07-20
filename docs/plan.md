# Plan: Velocity as a Cowork plugin marketplace

> Written 2026-07-20 after client feedback on the PoC. This is the "real project" start.
> Supersedes the delivered PoC (core + four surfaces + Clerk auth), which stays as-is.
> Classification: public (`docs/memory.md`) — any provider.

## Goal

Turn the repo into a **Claude Code plugin marketplace** and prove the whole loop in **Claude
Cowork**: add the marketplace → enable the Velocity plugin → use its skill + tools → authenticate
once via Clerk → auth persists across sessions. One plugin bundles both the **skill** and the
**remote MCP server**.

Done looks like: a screen-recordable Cowork session where the client installs the marketplace,
enables the plugin, asks a World Cup / currency question, hits the Clerk sign-in once, and it keeps
working afterward — including the per-user gating (allowlisted users get currency, others don't).

## What changes vs. the PoC

- **Cowork runs in the cloud.** Its sandbox can't reach `localhost`, a hand-run tunnel, or any
  stdio/local server. So:
  - The **Remote MCP must be deployed to a stable public HTTPS URL** (Fly.io, chosen).
  - The **CLI and Local MCP surfaces drop out of this path** — they can't be reached from Cowork.
    They remain valid local-user surfaces (see `docs/secure-local-surfaces.md`) but are not part of
    the marketplace/Cowork demo. The demo surfaces are **Remote MCP + Skill**.
- **Distribution shifts** from `npx skills add` + manual connector → a marketplace the client adds
  once, offering plugins that bundle skill + MCP together.

## Context / constraints

- Reuse everything that exists: `Velocity.Mcp.Core`, both tools, `Velocity.Mcp.Server`, the skill.
  No tool-contract changes. This is packaging + deployment + auth hardening, not a rewrite.
- **"Auth once, persisted" is mostly free** — Claude Code/Cowork persists remote-MCP OAuth tokens
  and auto-refreshes them. We inherit requirement #3 from the platform.
- The repo is public — no secrets committed. Prod config (issuer, audience, allowlist) lives in
  Fly secrets.
- .NET 10, ASP.NET. Frankfurter (no key). SQLite in-memory. Unchanged.

## Known vs. must-verify

Some plugin/Cowork mechanics below came from a docs research pass and need confirming against the
live docs + a real Cowork run before we trust them:

- **VERIFY:** exact shape of the plugin `mcpServers` OAuth block (`oauth.clientId` / `callbackPort`
  / `scopes` / `authServerMetadataUrl`) against the current plugins reference.
- **VERIFY:** the reported DCR bug (tokens discarded on launch, tracked as a GH issue) — if real,
  it breaks "auth once" and is the reason to pre-register a client (step 2). Confirm before relying
  on DCR either way.
- **VERIFY:** Cowork's marketplace-install UI/flow and that project-scope vs user-scope plugins
  load in the sandbox. Docs suggest user-scope/marketplace install is the safe path.
- **KNOWN-GOOD:** marketplace = `.claude-plugin/marketplace.json` at repo root; plugin =
  `.claude-plugin/plugin.json` bundling `skills` + `mcpServers`; remote HTTP MCP + OAuth is
  supported and tokens persist. Consistent across sources.

## Steps

1. [ ] **Deploy the Remote MCP to Fly.io (public HTTPS).**
   - Add a `Dockerfile` for `Velocity.Mcp.Server` (multi-stage, `mcr.microsoft.com/dotnet/aspnet:10.0`).
   - `fly launch` → stable `https://<app>.fly.dev`. Bind Kestrel to `0.0.0.0:8080`; Fly terminates TLS.
   - Fly secrets: `Clerk__Authority`, `Velocity__FullAccessEmails__0`, `Mcp__Resource=https://<app>.fly.dev`.
   - The forwarded-headers handling we already added makes the OAuth discovery URLs come out as the
     public https origin. Verify: `/.well-known/oauth-protected-resource` reports the fly URL; the
     401 points at it.

2. [ ] **Harden auth for public, multi-user distribution.**
   - **Pre-register a Clerk OAuth client** (public/PKCE) instead of relying on DCR per install.
     Ship its `client_id` in the plugin's MCP OAuth config. More robust for a distributed plugin and
     sidesteps the DCR-token-discard risk.
   - **Close the audience gap** (`docs/decisions.md`, 2026-07-16): confirm what `aud` a pre-registered
     client's token carries, then set `Clerk:Audience` and re-enable audience validation. Delete the
     conditional branch in `Program.cs`. This is now in scope — a public endpoint installable by
     strangers is exactly the confused-deputy scenario the branch warned about.
   - Keep DCR available as a fallback only if the client story needs it.

3. [ ] **Add the marketplace + plugin manifests.**
   - `/.claude-plugin/marketplace.json` at repo root: name, owner, one `plugins[]` entry.
   - A plugin dir (e.g. `plugins/velocity/`) with `.claude-plugin/plugin.json`:
     - `skills` → the skill (must live **inside** the plugin dir — installed plugins are cached and
       can't reference files outside themselves, so relocate/copy `skills/velocity/` under the plugin).
     - `mcpServers` → `{ "velocity": { "type": "http", "url": "https://<app>.fly.dev", "oauth": {…} } }`
       with the pre-registered `client_id`.
   - Decide plugin granularity — see the open client decision below.

4. [ ] **Validate the full loop in Cowork.**
   - Add the marketplace, enable the plugin, invoke the skill/tools.
   - Confirm OAuth runs **once** and persists across a new Cowork session.
   - Re-show per-user gating in Cowork: allowlisted account sees `convert_currency`; a non-allowlisted
     one sees only `get_world_cup_tournaments`.
   - Record it for the client.

## Open decision (needs the client)

**Plugin granularity.** "Enable the specific plugins I'm interested in" implies ≥2 plugins to choose
between. But plugin-enablement (client-side discovery) and our per-user gating (server-side security)
are different axes — enabling a "currency" plugin doesn't grant currency if the server denies you.
Options:
- **(a) One `velocity` plugin** (skill + both tools, server-gated) — matches what we built; "pick and
  choose" then means Velocity vs. other marketplaces' plugins.
- **(b) Two+ plugins** to make selection visible — e.g. split the skill into per-domain slices. Adds
  packaging work and needs a story for how enablement relates to the single shared MCP endpoint.

Recommend (a) for the first Cowork demo; revisit (b) if the client specifically wants the multi-plugin
selection on screen. Confirm before step 3.

## Files in scope

| Path | What |
|---|---|
| `src/Velocity.Mcp.Server/Dockerfile` | new — containerize for Fly |
| `fly.toml` | new — Fly app config |
| `src/Velocity.Mcp.Server/Program.cs` | close audience gap; pre-registered client handling |
| `.claude-plugin/marketplace.json` | new — the marketplace |
| `plugins/velocity/**` | new — plugin manifest + bundled skill + MCP config |
| `skills/velocity/` | relocate/copy under the plugin dir (cache isolation) |
| `docs/architecture.md`, `docs/decisions.md`, `docs/memory.md` | update alongside |

## Out of scope

- CLI and Local MCP surfaces — can't be reached from Cowork; unchanged, not in this demo.
- Tool contracts / `[Description]` text — no changes.
- The `Custom APIs` backend layer — still skipped; the tools stay on public Frankfurter + SQLite.
- Multi-region / autoscaling / CI — a single Fly instance is enough for a demo.

## Verification

- `curl https://<app>.fly.dev/health` → `{"status":"ok"}`; a no-token MCP call → 401 pointing at the
  fly resource-metadata URL.
- Audience validation **on**: a token minted for a different resource is rejected (the gap is closed).
- In Cowork: marketplace adds, plugin enables, first invoke triggers Clerk sign-in once, a fresh
  session reuses the token with no re-auth.
- Per-user gating holds in Cowork identically to the localhost demo.
