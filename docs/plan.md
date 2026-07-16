# Handoff plan (Claude Code -> OpenCode)

> Written by Claude Code (Max subscription) during heavy planning/reasoning.
> OpenCode reads this and executes it with open models. Source of truth for intent.
> Delete or archive when complete.
>
> Classification is **public** (docs/memory.md) — any provider, including free Zen models.

## Goal
PoC of the MCP Platform Architecture (`mcp-platform-architecture.pdf`): one shared core library behind four surfaces, from one repo, exposing two tools (World Cup lookup, currency conversion).

**Phase 1 is complete and verified** — core + both tools + self-check. This plan covers phases 2-6.

## Context / constraints
- .NET 10, C#, ASP.NET Minimal APIs. SQLite for DB work. Any frontend: vanilla JS/HTML/CSS, no frameworks.
- **Tool contracts live in `Velocity.Mcp.Core` only.** Surfaces call `AddVelocityCore()` and bind core's `[McpServerToolType]` classes. A surface that redefines a tool schema is a bug, not a style choice.
- Every surface pins an exact `Velocity.Mcp.Core` version via `Directory.Packages.props`.
- MCP C# SDK is 1.4.1 (2.0 is preview — do not adopt without asking).
- Frankfurter needs no API key. Do not introduce one.
- Verify against a real MCP client, not just a passing build.

## Steps
1. [x] **Phase 1 — core.** `Velocity.Mcp.Core` with both tools, SQLite World Cup, live FX, `AddVelocityCore()`, and a self-check. Done 2026-07-16, 13/13 checks pass.
2. [x] **Phase 2 — Remote MCP host.** `src/Velocity.Mcp.Server`, ASP.NET Minimal API, `ModelContextProtocol.AspNetCore` 1.4.1, streamable HTTP. Program.cs should be roughly: `AddVelocityCore()`, `AddMcpServer().WithHttpTransport().WithTools<WorldCupTools>().WithTools<CurrencyTools>()`, `MapMcp()`. No auth yet. Add a `/health` endpoint. Done 2026-07-16; verified against a live client (initialize, tools/list, both tools called, error paths).
3. [x] **Phase 3a — Local MCP (stdio).** `src/Velocity.Mcp.Local`, same binding as phase 2 but `WithStdioServerTransport()`. **Nothing may write to stdout** — stdout is the protocol channel. Route logs to stderr. Done 2026-07-16; verified by driving it over a real stdin/stdout pipe — stdout stayed pure JSON-RPC.
4. [x] **Phase 3b — CLI.** `src/Velocity.Mcp.Cli` as a dotnet tool (`PackAsTool`, command `velocity`). Subcommands: invoke each tool directly for scripts/CI, plus `velocity mcp install` / `velocity mcp remove` which write/merge/reverse the `.mcp.json` entry pointing at the phase-3a stdio binary. Merge into existing `.mcp.json` — never clobber a user's other servers. Done 2026-07-16. **Deviation: arg parsing is hand-rolled, not `System.CommandLine`** — see ADR.
5. [ ] **Phase 4 — Skill.** `skills/velocity/SKILL.md` — prompt + docs describing the two tools. No C#. CI packages it to `skill.zip` for GitHub Releases.
6. [ ] **Phase 5 — Auth.** OAuth 2.1 + PKCE on the Remote MCP surface only. Host validates access tokens via JWKS and never sees credentials; the MCP client runs the flow. **Provider not yet chosen — ask before starting.**
7. [ ] **Phase 6 — CI.** GitHub Actions: tag → build → test → publish NuGet + attach `skill.zip` to the release.

## Files in scope
| Path | What changes |
|---|---|
| `src/Velocity.Mcp.Server/` | new — phase 2 |
| `src/Velocity.Mcp.Local/` | new — phase 3a |
| `src/Velocity.Mcp.Cli/` | new — phase 3b |
| `skills/velocity/` | new — phase 4 |
| `.github/workflows/` | new — phase 6 |
| `Directory.Packages.props` | add packages as needed |
| `Velocity.slnx` | add each new project |
| `docs/architecture.md`, `docs/decisions.md` | update in the same change as the code |

## Delegation
- Locate first with `@explore`: read `src/Velocity.Mcp.Core/` before touching anything — the binding pattern is the whole point.
- Parallel (`@worker`, disjoint files): steps 2, 3a, 3b are independent once core is pinned. Step 5 is not — it modifies step 2's output.
- Sequential: 4 after 3b (the skill documents the install flow). 6 last. 5 blocked on a provider decision.
- `@review` the combined diff before finalizing.

## Out of scope / do NOT touch
- `src/Velocity.Mcp.Core/` tool signatures and `[Description]` text — changing them changes the contract for every surface at once. Propose, don't edit.
- Auth on any surface other than Remote MCP. Skill, Local MCP and CLI run as the user, by design (see the diagram's trust boundary).
- The `Custom APIs` layer from the diagram. Deliberately skipped — see ADR 2026-07-16.
- Frontend. Explicitly not wanted yet.
- MCP SDK 2.0 preview.

## Verification
- `dotnet build` — must be clean, including **zero NU1903** advisories.
- `dotnet run --project tests/Velocity.Mcp.Core.SelfCheck` — must print `All checks passed.` and exit 0. Needs network (live FX).
- Phase 2: `curl` the health endpoint, then attach a real MCP client and confirm both tools list and execute.
- Phase 3b: run `velocity mcp install` against a `.mcp.json` that already contains an unrelated server; confirm the existing entry survives and `remove` reverses cleanly.
- Extend the self-check alongside any new logic. Asserts in the console app, no test framework unless asked.
- **Tools must throw `McpException` for anything the caller should read.** Every other exception type is replaced by the SDK with a generic string. Assert on message content, not exception type. See ADR 2026-07-16.
