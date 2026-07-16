# Velocity

A proof-of-concept MCP platform: **one shared core library, four surfaces, one repo.**
Two tools — FIFA World Cup lookup and currency conversion — implemented once in a core
library and exposed through a remote MCP server, a local stdio MCP server, a CLI, and a
Skill. Built from the architecture in [`mcp-platform-architecture.pdf`](mcp-platform-architecture.pdf).

The point of the shape: a tool is defined in exactly one place. `velocity convert 100 USD EUR`
on the command line and the `convert_currency` MCP tool run the *same code*. No surface
redefines a tool; each is a thin adapter over the core.

```
                    ┌─────────────────────────────────────────────┐
   Remote MCP  ─────┤                                             │
   Local MCP   ─────┤   Velocity.Mcp.Core                         │──► SQLite (World Cup)
   CLI         ─────┤   tool contracts · validation · auth policy │──► Frankfurter (ECB FX)
   Skill (docs)─────┤                                             │
                    └─────────────────────────────────────────────┘
```

## The two tools

| Tool | What it does | Backed by |
|---|---|---|
| `get_world_cup_tournaments` | Every men's World Cup 1930–2026: host, winner, runner-up, score, venue, attendance. Filter by year, team, or host. | In-memory SQLite, seeded at startup |
| `convert_currency` | Convert between ~30 currencies at ECB reference rates, today or on a past date. | [Frankfurter](https://frankfurter.dev) (no API key) |

## Repository layout

| Path | What |
|---|---|
| `src/Velocity.Mcp.Core` | The shared core: both tool contracts, SQLite data, FX client, auth policy name |
| `src/Velocity.Mcp.Server` | **Remote MCP** — ASP.NET Minimal API, streamable HTTP, OAuth via Clerk |
| `src/Velocity.Mcp.Local` | **Local MCP** — stdio server for a dev machine |
| `src/Velocity.Mcp.Cli` | **CLI** — `dotnet tool`; runs the tools and installs the local server into `.mcp.json` |
| `skills/velocity` | **Skill** — `SKILL.md`, prompt + docs, no code |
| `tests/Velocity.Mcp.Core.SelfCheck` | Runnable self-check (asserts, no test framework) |
| `docs/` | Living docs: architecture, decisions (ADRs), db schema, memory |

## Prerequisites

- **.NET 10 SDK** (`dotnet --version` ≥ 10.0)
- For the remote server only: a **Clerk** account (OAuth authorization server)

## Quick start

```bash
dotnet build
dotnet run --project tests/Velocity.Mcp.Core.SelfCheck   # exercises both tools; needs network for FX
```

The self-check prints `All checks passed.` and exits 0. It hits the live FX API, so it needs
a network connection.

## Running each surface

### CLI

```bash
dotnet run --project src/Velocity.Mcp.Cli -- worldcup --team Brazil
dotnet run --project src/Velocity.Mcp.Cli -- convert 100 USD EUR
dotnet run --project src/Velocity.Mcp.Cli -- convert 100 USD EUR --date 2024-01-15
```

Packaged as a `dotnet tool` named `velocity`. It can also register the local MCP server for you:

```bash
velocity mcp install     # merges an entry into ./.mcp.json (leaves other servers intact)
velocity mcp remove      # reverses it
```

### Local MCP (stdio)

```bash
dotnet run --project src/Velocity.Mcp.Local
```

Speaks JSON-RPC over stdin/stdout. Point any stdio MCP client at the built
`velocity-mcp-local` executable, or use `velocity mcp install` to wire it into `.mcp.json`.
Runs as the current user with no authentication — the same trust model as the CLI.

### Remote MCP (HTTP + OAuth)

This is the only surface behind an auth boundary. It needs Clerk configured — see below.

```bash
cd src/Velocity.Mcp.Server
dotnet user-secrets set "Clerk:Authority" "https://YOUR-INSTANCE.clerk.accounts.dev"
dotnet user-secrets set "Velocity:FullAccessEmails:0" "you@example.com"
cd ../..
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Velocity.Mcp.Server
```

Then point an MCP client (e.g. Claude Code) at `http://localhost:5199` — a ready-made
[`.mcp.json`](.mcp.json) does this. The client runs the OAuth flow; you sign in through Clerk.

### Skill

The skill is prompt + docs, installable straight from this repo:

```bash
npx skills add ovunc-gursoy/velocity_poc -s velocity
```

Or upload `skills/velocity` as a `skill.zip` in the Claude UI.

### Remote MCP as a claude.ai connector

claude.ai connects from its own servers, so it needs a **public HTTPS URL** — `localhost`
won't work. For a demo, tunnel the local server; for anything lasting, deploy it.

```bash
# 1. run the server (see Remote MCP above), then expose it — e.g. an account-less quick tunnel:
cloudflared tunnel --url http://localhost:5199        # prints https://<random>.trycloudflare.com

# 2. restart the server telling it its public identity (the resource id must match the tunnel URL):
Mcp__Resource="https://<random>.trycloudflare.com" ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/Velocity.Mcp.Server
```

Then in claude.ai: **Settings → Connectors → Add custom connector**, paste the tunnel URL, and
sign in through Clerk when prompted. The server honors `X-Forwarded-*`, so the OAuth discovery
URLs come out as the public `https` origin automatically.

> **Exposure note:** a tunnel puts this on the public internet, which widens the audience gap
> below. It stays low-risk *only* because the data is read-only and public and the Clerk instance
> is a single-tenant dev one — a random Clerk user would get the World Cup tool and nothing more.
> Don't tunnel anything with real data on this basis.

## Authentication & per-user tool access

The remote server uses **Clerk** as an OAuth 2.1 + PKCE authorization server. The MCP client
runs the flow; **the server never sees credentials** — it validates the access token's
signature against Clerk's JWKS, discovered from `Clerk:Authority`. An unauthenticated request
gets a `401` carrying an RFC 9728 resource-metadata pointer, which is how a client discovers
where to authenticate.

**Per-user tools.** Tool availability depends on who you are:

| Caller | `get_world_cup_tournaments` | `convert_currency` |
|---|---|---|
| Email in `Velocity:FullAccessEmails` | ✅ | ✅ |
| Any other authenticated user | ✅ | ❌ hidden and rejected |

This is enforced by the MCP SDK's authorization filters against an `[Authorize]` policy on the
currency tool: unauthorized callers don't see it in `tools/list` *and* can't call it. The
caller's email is resolved from Clerk's `userinfo` endpoint (the access token itself carries no
email) and cached. The allowlist is configuration, never committed.

### Clerk setup

1. Create an application in the [Clerk dashboard](https://dashboard.clerk.com/).
2. Enable **Dynamic Client Registration** (OAuth applications) — MCP clients need it to
   self-register during the flow.
3. Copy the **Frontend API URL** (`https://…clerk.accounts.dev`) into `Clerk:Authority`.

## Security posture

This is a PoC. It is honest about its limits — see the ADRs in
[`docs/decisions.md`](docs/decisions.md). The load-bearing one:

> **Audience validation is off, because Clerk issues these tokens with no `aud` claim and
> advertises no RFC 8707 resource binding.** A token minted for a *different* MCP server on the
> same Clerk instance would be accepted here. This is safe only for a single-tenant dev instance
> on localhost, over read-only public data. It must be closed before any deployment — the code
> and the ADR spell out how.

Other productionizing work (HTTPS/deployment, rate limiting, telemetry) is deliberately out of
scope for the PoC and listed in the architecture doc.

## Notable design decisions

The diagram is npm/TypeScript-shaped; this is .NET. Where they diverge, the reasoning is
recorded as ADRs in [`docs/decisions.md`](docs/decisions.md):

- **NuGet + `dotnet tool`** replace npm/`npx` — same topology, native tooling.
- **Core owns the MCP tool attributes**, so surfaces bind and never redefine them.
- **Tools throw `McpException`**, the one exception type the SDK relays to the caller —
  validation messages are written for an agent to read and self-correct.
- **In-memory SQLite, seeded on boot, no repository abstraction** — 23 static rows.
- **Frankfurter** for FX — no API key to leak from a public repo.

## Development

```bash
dotnet build                                              # zero warnings, incl. no NU1903 advisories
dotnet run --project tests/Velocity.Mcp.Core.SelfCheck    # the self-check is the test suite
```

The self-check uses plain asserts in a console app — no framework. Extend it alongside any new
logic; assert on behavior, not just exception types.
