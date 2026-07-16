# Project memory

> Durable facts about this project that aren't obvious from the code or git history.
> Keep terse. Update when something non-obvious is learned.

## What this is
- Purpose: PoC for the MCP Platform Architecture (see `mcp-platform-architecture.pdf`) — one shared core library behind four surfaces, shipped from one repo. Two demo tools: World Cup lookup and currency conversion.
- Stack: .NET 10 (SDK 10.0.301), C#, ASP.NET Minimal APIs, SQLite, MCP C# SDK 1.4.1. Frontend, if ever: vanilla JS/HTML/CSS, no frameworks.
- Entry points: `src/Velocity.Mcp.Core` (the only place tool contracts are defined). Self-check: `dotnet run --project tests/Velocity.Mcp.Core.SelfCheck`.

## Data sensitivity
> Controls which model providers may see this code (see global AGENTS.md §6).
- Classification: public
- Allowed providers: any, including free Zen models.

## Conventions / gotchas
- **Tool contracts live in core, never in a surface.** The diagram is explicit: surfaces never redefine tools. Surfaces call `AddVelocityCore()` and bind core's `[McpServerToolType]` classes — that's it.
- Every surface pins an exact `Velocity.Mcp.Core` version. Contracts change only when core majors.
- The diagram says npm/`@scope/*`/`npx`. We use **NuGet + dotnet tools** instead — same topology, right nouns for .NET. Skill still ships as `skill.zip` via GitHub Releases (that's Claude-UI-side, stack-agnostic). See ADR 2026-07-16.
- `Microsoft.Data.Sqlite` transitively resolves a SQLite with a high-severity advisory; `SQLitePCLRaw.bundle_e_sqlite3` is pinned past it in `Directory.Packages.props`. Re-check on upgrade.
- Tool XML `[Description]` text is the agent's only documentation — it is a real interface, not a comment. Edit with the same care as a signature.

## External services & integrations
- **Frankfurter** (`https://api.frankfurter.dev/v1/`) — ECB reference rates. No API key, no signup, no quota to manage. Daily rates only, not live/intraday.
  - Same-currency pair (`base == symbol`) returns HTTP 422; core short-circuits before calling.
  - Weekend/holiday dates silently roll back to the previous working day — the response's `date` is the authority, so we surface it as `RateDate`.
  - Unknown currency code returns HTTP 404 with `{"message":"not found"}`.
- No auth provider yet. OAuth 2.1 + PKCE is phase 5.

## Open questions
- Hosting target for the Remote MCP surface: the diagram lists Azure Container Apps / Fly.io / Cloudflare. Not chosen.
- OAuth provider for phase 5: Clerk / Auth0 / Okta / Entra ID / Keycloak. Not chosen.
