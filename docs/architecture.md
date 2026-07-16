# Architecture

> Components, data flow, and decisions currently in effect. Update when structure changes.
> Derived from `mcp-platform-architecture.pdf`. Phase 1 of 6 is built; the rest is planned shape.

## Components

| Component | Responsibility | Key files | Status |
|---|---|---|---|
| `Velocity.Mcp.Core` | Tool contracts, validation, mapping, auth context. One implementation behind every surface. | `src/Velocity.Mcp.Core/` | **built** |
| `Velocity.Mcp.Server` | Remote MCP, streamable HTTP. The only surface behind a network + OAuth boundary. | `src/Velocity.Mcp.Server/Program.cs` | **built** |
| `Velocity.Mcp.Local` | Local MCP over stdio, runs on the dev machine as the user. | — | phase 3 |
| `Velocity.Mcp.Cli` | dotnet tool for scripts/CI. Also `mcp install` → writes/merges `.mcp.json`. | — | phase 3 |
| Skill | Prompt + docs. Ships as `skill.zip` via GitHub Releases. No C#. | — | phase 4 |

### Inside core
- `WorldCupTools` / `CurrencyTools` — the two `[McpServerToolType]` contracts. The `[Description]` strings are the agent-facing interface.
- `WorldCupDb` — in-memory SQLite, seeded from embedded `worldcup.sql` at construction. Singleton.
- `ServiceCollectionExtensions.AddVelocityCore()` — the single registration entry point every surface calls.

## Data flow
- Tool call → core tool method → either `WorldCupDb` (in-process SQLite) or Frankfurter over HTTP.
- Phase 1 talks to Frankfurter directly. The diagram routes core → Custom APIs (ASP.NET/APIM) → DBs/services; we have no domain API worth standing up yet, so core calls the upstream itself. Insert that layer when a second consumer or a non-trivial mapping appears — not before.

## Cross-cutting concerns
- **Auth:** none yet (deferred to phase 5). The trust boundary the diagram draws still holds by construction: Remote MCP is the only surface intended to sit behind network + OAuth; Skill, Local MCP and CLI run as the user with the user's own credentials.
- **Logging / telemetry:** none yet. Nothing to observe until there's a host.
- **Error handling:** tools throw **`McpException`** on bad input. This is not a style preference: the SDK propagates `McpException.Message` to the caller and replaces every other exception type with a generic "An error occurred invoking 'x'", which would strip the guidance an agent needs to correct its own call. Messages are written for an agent to read and self-correct, not for a log. Corollary: never put anything sensitive in an `McpException` message — it crosses the trust boundary by design. Use any other exception type for failures the caller shouldn't see.
- **Validation:** at the tool boundary, which is the trust boundary — MCP tool arguments are model-generated and untrusted. Currency codes, amounts and dates are checked before any upstream call. SQLite access is fully parameterised.

## Decisions in effect
> Summaries only; full rationale lives in decisions.md.
- NuGet + dotnet tools replace the diagram's npm/npx distribution.
- Core owns the MCP tool attributes directly, so surfaces bind rather than redefine.
- In-memory SQLite seeded on boot; no file, no migrations, no repository abstraction.
- Frankfurter for FX: no key, no secret to leak from a public repo.
