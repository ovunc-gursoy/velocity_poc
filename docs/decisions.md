# Decision log (ADRs)

> Append a new entry whenever a real trade-off is decided. Newest on top.

## 2026-07-16 — Clerk as authorization server; audience validation deferred with a known gap
- Status: accepted (provisional — revisit before any deployment)
- Context: Clerk is the chosen OAuth provider. Two findings shaped this: (1) Clerk has no custom scopes yet, only `openid`/`profile`/`email`/`public_metadata`/`private_metadata`/`user:org:read`, so a `velocity:tools` scope cannot exist; (2) Clerk does not document support for the RFC 8707 `resource` parameter, which is what binds an access token to a specific resource server. Clerk's own MCP helpers (`@clerk/mcp-tools`) are TypeScript-only and irrelevant to us — Clerk is a standard OAuth 2.1 AS issuing JWTs, so .NET's `JwtBearer` plus the MCP SDK's `AddMcp()` is all that's required.
- Decision: Validate signature, issuer and lifetime. Validate audience only when `Clerk:Audience` is configured; leave it unset for the localhost PoC. Authorization means "authenticated Clerk user". The host fails to start if `Clerk:Authority` is missing, rather than silently running open.
- Consequences: **Without audience binding this host would accept a token minted for a different MCP server on the same Clerk instance — a confused-deputy hole.** Tolerable strictly because the instance is single-tenant, in development, and bound to localhost. Before deploying anywhere: inspect a real Clerk token, confirm what lands in `aud`, set `Clerk:Audience`, and delete the conditional branch. Marked with a `ponytail:` comment at the validation site so it cannot be mistaken for a considered simplification.

## 2026-07-16 — Skill ships one way (skill.zip), not two
- Status: accepted
- Context: The diagram has the skill shipping twice from one source dir — `@scope/skill` on npm for agents and CI, and `skill.zip` from GitHub Releases for manual upload into the Claude UI. We already replaced npm with NuGet for the code surfaces, but a skill is pure markdown, so a NuGet package is not its natural agent-facing channel either — nothing would `dotnet tool install` a folder of prose.
- Decision: One source dir, `skills/velocity/`, packaged to `skill.zip` in phase 6 CI. The second channel is deferred until there's a consumer asking for it.
- Consequences: Manual upload is the only install path for now. Adding a second package step later is cheap precisely because the source dir stays single — which is the part of the diagram's advice that actually mattered. Revisit if agents need to install the skill unattended.

## 2026-07-16 — Hand-rolled CLI arg parsing instead of System.CommandLine
- Status: accepted
- Context: The plan called for `System.CommandLine` (2.0.10 is now stable). The CLI has four commands.
- Decision: Parse args by hand; a `Usage` const supplies help.
- Consequences: No dependency and no exposure to that library's parser API churn, at the cost of tab completion, typed option validation, and generated help. Four commands do not earn it. Revisit past roughly six commands, or if completion is asked for. Marked with a `ponytail:` comment at the parse site.

## 2026-07-16 — `mcp install` merges via JsonNode and refuses malformed files
- Status: accepted
- Context: `.mcp.json` is the user's file and generally holds other servers. A typed model would round-trip only the fields it knows about, silently dropping anything else.
- Decision: Parse to `JsonNode`, mutate only the one entry, write back. On invalid JSON, throw rather than start from an empty object. `remove` leaves an empty `mcpServers` map rather than deleting the key or file.
- Consequences: Unknown keys and unrelated servers survive by construction, and a corrupt config is reported instead of clobbered. Covered by self-checks, including one asserting a malformed file is left byte-for-byte unchanged.

## 2026-07-16 — Tool validation errors throw McpException, not ArgumentException
- Status: accepted
- Context: Phase 1 threw `ArgumentException` / `ArgumentOutOfRangeException` with messages written for an agent to self-correct. Driving the phase 2 host with a real MCP client showed the client only ever received `"An error occurred invoking 'convert_currency'."` — the SDK deliberately hides exception messages, propagating only `McpException.Message`, so nothing leaks by accident.
- Decision: All caller-facing validation in core throws `McpException`. The self-check asserts on message *content*, not just exception type, so a regression to a silent generic error fails the check.
- Consequences: Core's exception types are now dictated by the SDK's disclosure model, which is the right trade — the message is a real part of the tool interface, and an error an agent can't read is a dead end for it. The flip side is that `McpException` messages cross the trust boundary by construction: nothing sensitive may go in one. Any failure the caller shouldn't see must use a different exception type, which then surfaces as the generic string. Noted in architecture.md.

## 2026-07-16 — Core owns the MCP tool attributes; surfaces bind, never redefine
- Status: accepted
- Context: The diagram puts "tool contracts" in the core box and states surfaces never redefine them. The alternative is core exposing plain C# methods with each surface applying its own `[McpServerTool]` metadata.
- Decision: `[McpServerToolType]` / `[McpServerTool]` / `[Description]` live in `Velocity.Mcp.Core`. Surfaces call `AddVelocityCore()` and bind core's types.
- Consequences: Core takes a dependency on the MCP SDK, which the CLI and Skill surfaces don't strictly need. Accepted — it's the only way one tool schema reaches every surface. The alternative guarantees drift the moment two surfaces disagree about a description.

## 2026-07-16 — NuGet + dotnet tools instead of the diagram's npm distribution
- Status: accepted
- Context: The diagram is npm-shaped (`@scope/core`, `npx @scope/cli`, npm registry). The stack is .NET 10.
- Decision: Core ships as a NuGet package; CLI and Local MCP ship as dotnet tools. Skill still ships as `skill.zip` from GitHub Releases.
- Consequences: `npx @scope/cli mcp install` becomes a dotnet tool invocation. Losing `npx` reach for JS-native agents was considered; wrapping .NET binaries in per-platform npm shims is real work for a PoC. Revisit if agent installability via npm turns out to matter.

## 2026-07-16 — Frankfurter for FX rates
- Status: accepted
- Context: Needed a live FX source. Alternatives (exchangerate-api, Fixer, Open Exchange Rates) all require an API key.
- Decision: Frankfurter — ECB reference rates, no key, no signup.
- Consequences: Daily rates only, not intraday; ~30 currencies, no crypto. No secret can leak from a public repo, and there's no key provisioning step to onboard anyone. Swap behind the existing core contract if intraday or wider coverage is needed — `Conversion` already reports the rate date, so callers can't mistake a daily rate for live.

## 2026-07-16 — In-memory SQLite, seeded on boot, no abstraction
- Status: accepted
- Context: The World Cup dataset is 23 static, read-only rows.
- Decision: In-memory SQLite seeded from an embedded `.sql` file in the `WorldCupDb` constructor. No repository interface, no migrations, no ORM.
- Consequences: Re-seeds per process (trivial at this size) and there's nothing to persist or migrate. An interface over one implementation would be pure ceremony. Move to a file-backed source if the data grows or gains writes; the `ponytail:` comment on `WorldCupDb` marks the spot.

## 2026-07-16 — Core calls Frankfurter directly, skipping the diagram's Custom APIs layer
- Status: accepted
- Context: The diagram routes core → Custom APIs (ASP.NET Core / APIM) → DBs and services. For two tools there is no domain logic to put in that layer.
- Decision: Core calls the upstream directly for now.
- Consequences: Deviates from the diagram. The layer earns its place when there's a second consumer, real mapping, or a credential that must not reach the client — none apply yet. Flagged here so the deviation is deliberate rather than forgotten.
