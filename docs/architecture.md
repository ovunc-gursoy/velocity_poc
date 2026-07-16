# Architecture

> Components, data flow, and decisions currently in effect. Update when structure changes.
> Derived from `mcp-platform-architecture.pdf`. Phases 1-5 of 6 are built (phase 5 pending a live Clerk instance); the rest is planned shape.

## Components

| Component | Responsibility | Key files | Status |
|---|---|---|---|
| `Velocity.Mcp.Core` | Tool contracts, validation, mapping, auth context. One implementation behind every surface. | `src/Velocity.Mcp.Core/` | **built** |
| `Velocity.Mcp.Server` | Remote MCP, streamable HTTP. The only surface behind a network + OAuth boundary. | `src/Velocity.Mcp.Server/Program.cs` | **built** |
| `Velocity.Mcp.Local` | Local MCP over stdio, runs on the dev machine as the user. | `src/Velocity.Mcp.Local/Program.cs` | **built** |
| `Velocity.Mcp.Cli` | dotnet tool for scripts/CI. Also `mcp install` → writes/merges `.mcp.json`. | `src/Velocity.Mcp.Cli/` | **built** |
| Skill | Prompt + docs. Ships as `skill.zip` via GitHub Releases. No C#. | `skills/velocity/SKILL.md` | **built** |

### Inside core
- `WorldCupTools` / `CurrencyTools` — the two `[McpServerToolType]` contracts. The `[Description]` strings are the agent-facing interface.
- `WorldCupDb` — in-memory SQLite, seeded from embedded `worldcup.sql` at construction. Singleton.
- `ServiceCollectionExtensions.AddVelocityCore()` — the single registration entry point every surface calls.

### Surface notes
- **Local MCP:** stdout *is* the JSON-RPC channel. All logging is redirected to stderr in `Program.cs`; a stray `Console.WriteLine` there corrupts the stream and the client drops the connection.
- **Skill:** markdown only, no code. It documents *when* to reach for the tools and the traps in the data — historical team names, the unplayed 2026 tournament, ECB rates being daily rather than live. Its factual claims are checkable, so the self-check pins the one most likely to rot (the Germany / West Germany title split). All four surfaces are now built, so the diagram's shape is fully realised.
- **CLI:** calls core in-process, same as the MCP surfaces — `velocity convert` and the `convert_currency` tool run identical code. `mcp install` merges into `.mcp.json` via `JsonNode` so unrelated servers and unknown keys survive; it refuses to touch a malformed file rather than overwrite it.

## Data flow
- Tool call → core tool method → either `WorldCupDb` (in-process SQLite) or Frankfurter over HTTP.
- Phase 1 talks to Frankfurter directly. The diagram routes core → Custom APIs (ASP.NET/APIM) → DBs/services; we have no domain API worth standing up yet, so core calls the upstream itself. Insert that layer when a second consumer or a non-trivial mapping appears — not before.

## Cross-cutting concerns
- **Auth:** OAuth 2.1 + PKCE on `Velocity.Mcp.Server` only, with **Clerk** as the authorization server. The MCP client runs the flow; the host never sees credentials and only validates the access token's signature against Clerk's JWKS (`{authority}/.well-known/jwks.json`), which it discovers via `Authority`. A 401 carries `WWW-Authenticate: Bearer resource_metadata="..."` pointing at an RFC 9728 document at `/.well-known/oauth-protected-resource`; that is how a client discovers Clerk. `/health` is deliberately anonymous — a health probe that needs a token cannot report that auth is broken. Skill, Local MCP and CLI stay unauthenticated by design: they run as the user with the user's own credentials, exactly as the diagram's trust boundary describes.
  - **Known gap:** audience validation is disabled unless `Clerk:Audience` is set. Clerk does not document RFC 8707 resource binding, so a token minted for another MCP server on the same Clerk instance would be accepted. Acceptable only for a single-tenant dev instance on localhost. See ADR.
  - Clerk has no custom scopes yet, so authorization is "authenticated Clerk user", not per-tool consent.
  - **Per-user tool access.** The currency tool carries `[Authorize(Policy = CurrencyTools.FullAccessPolicy)]`; the World Cup tool is unpolicied. The host's `AddAuthorizationFilters()` enforces this for both `tools/list` (unauthorized tools are hidden) and `tools/call` (rejected). The policy checks an `email` claim against `Velocity:FullAccessEmails`. Since Clerk's token carries no email, the host resolves it from Clerk's `userinfo` endpoint on `OnTokenValidated` and caches it (`ClerkUserInfo`), failing closed — an unresolved email means World Cup only, never full access. The attribute lives in core (the diagram puts auth context there) but is inert on Local MCP and the CLI, which don't call the filter and run as the user.
- **Logging / telemetry:** none yet. Nothing to observe until there's a host.
- **Error handling:** tools throw **`McpException`** on bad input. This is not a style preference: the SDK propagates `McpException.Message` to the caller and replaces every other exception type with a generic "An error occurred invoking 'x'", which would strip the guidance an agent needs to correct its own call. Messages are written for an agent to read and self-correct, not for a log. Corollary: never put anything sensitive in an `McpException` message — it crosses the trust boundary by design. Use any other exception type for failures the caller shouldn't see.
- **Validation:** at the tool boundary, which is the trust boundary — MCP tool arguments are model-generated and untrusted. Currency codes, amounts and dates are checked before any upstream call. SQLite access is fully parameterised.

## Decisions in effect
> Summaries only; full rationale lives in decisions.md.
- NuGet + dotnet tools replace the diagram's npm/npx distribution.
- Core owns the MCP tool attributes directly, so surfaces bind rather than redefine.
- In-memory SQLite seeded on boot; no file, no migrations, no repository abstraction.
- Frankfurter for FX: no key, no secret to leak from a public repo.
