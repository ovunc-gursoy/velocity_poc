# Velocity — Client Demo Script

A demo in order, shortest path to the "wow". Roughly 15 minutes. Each step says what to run,
what the client sees, and the one sentence to say. The climax is **per-user tool access**
(step 6) — pace the earlier steps so you reach it with time to spare.

---

## 0. Before the client is in the room (pre-flight)

Do this ahead of time; none of it is worth watching live.

```bash
dotnet build                                              # clean, zero warnings
dotnet run --project tests/Velocity.Mcp.Core.SelfCheck    # prints "All checks passed."
```

Then:

- [ ] Clerk configured on the remote server (`Clerk:Authority` + `Velocity:FullAccessEmails` in user-secrets).
- [ ] Dynamic Client Registration is **on** in the Clerk dashboard.
- [ ] Remote server running: `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Velocity.Mcp.Server` — confirm `curl http://localhost:5199/health` returns `{"status":"ok"}`.
- [ ] A second Clerk test account exists (any email **not** in the allowlist) — needed for step 6.
- [ ] An MCP client ready (Claude Code in a terminal is easiest).
- [ ] Terminals pre-opened, font size up.

---

## 1. The one-sentence pitch (30 seconds, no screen)

> "Two tools — World Cup facts and currency conversion — written once, in one core library,
> and exposed four different ways: a command-line tool, a local agent server, a hosted server
> behind login, and a documentation skill. Nobody reimplements a tool per channel. Watch the
> *same* tool show up everywhere."

Keep it to that. The demo makes the point better than the slide would.

---

## 2. The CLI — instant and tangible (2 min)

Start here because it's zero-friction and the output is immediate.

```bash
dotnet run --project src/Velocity.Mcp.Cli -- worldcup --team Brazil
dotnet run --project src/Velocity.Mcp.Cli -- convert 100 USD EUR
```

- **They see:** Brazil's seven finals in a table; a live USD→EUR conversion with the ECB rate date.
- **Say:** "That currency number is a live European Central Bank rate, fetched just now. And this
  exact code is what every other surface runs — the CLI is just one doorway to it."

Optional, if asked "what if I get it wrong?":

```bash
dotnet run --project src/Velocity.Mcp.Cli -- convert 100 BANANAS EUR
```

- **They see:** a precise, human-readable error naming the bad input.
- **Say:** "Every surface gets that same guidance — it's written so an AI agent can correct itself."

---

## 3. Local MCP inside an agent (3 min)

Show an AI agent actually *using* the tool, with no setup ceremony.

```bash
velocity mcp install      # wires the local server into .mcp.json
```

Open your agent (Claude Code) in this folder. Ask it, in plain English:

> "Who won the 2018 World Cup, and what's 250 euros in Japanese yen?"

- **They see:** the agent call `get_world_cup_tournaments` and `convert_currency` and answer in prose.
- **Say:** "The agent discovered the tools, called them, and used real data — no code, just a
  natural question. This local server runs as me, on my machine, with no login. That's the right
  model for a developer's own tools."

---

## 4. Remote MCP + login — the trust boundary (3 min)

Now the hosted version, the one behind authentication.

Point the agent at the remote server (the repo's [`.mcp.json`](.mcp.json) already has `velocity-remote`
→ `http://localhost:5199`). Trigger the connection.

- **They see:** the agent gets bounced to a **Clerk sign-in page** in the browser; after they
  sign in and approve, it connects.
- **Say:** "The client just did a full OAuth login. Crucially, *my server never saw the password*
  — it only checks a signed token. And the client wasn't preconfigured with where to log in; it
  discovered that from the server's `401`. That's the standard MCP auth handshake, working."

If they're technical, show the handshake:

```bash
curl -s -D- -o /dev/null -X POST http://localhost:5199/ \
  -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | grep -i "www-authenticate"
```

- **Say:** "No token, so it's refused — and the refusal *tells the client where to authenticate*."

---

## 5. (Recap breath, 30 sec)

> "Same two tools: on the command line, inside a local agent, and now behind a hosted login —
> all from one implementation. Last thing, and it's the interesting one: the hosted server can
> give *different tools to different people*."

---

## 6. Per-user tool access — the climax (4 min)

**As the privileged user** (the account in `Velocity:FullAccessEmails`, e.g. yours):

Ask the connected agent:

> "What tools do you have from velocity-remote?"

- **They see:** both `get_world_cup_tournaments` and `convert_currency`.
- Then: "Convert 500 USD to GBP." → it works.

**Now as a different user.** Sign out of Clerk / use a second account that is **not** in the
allowlist, and reconnect the agent. Ask the same question:

> "What tools do you have from velocity-remote?"

- **They see:** **only** `get_world_cup_tournaments`. The currency tool is *gone*.
- Then: "Convert 500 USD to GBP." → the agent reports it has no such tool / it's refused.
- **Say:** "Same server, same URL. This user simply isn't authorized for the currency tool, so
  they don't even see it — it's hidden from the tool list *and* refused if called directly. Tool
  access is per-identity, decided by the server, enforced by the protocol."

That's the whole thesis landed: one core, many surfaces, and fine-grained, per-user control on
the surface that needs it.

> **Solo rehearsal without a second account:** you can prove the same thing with one account by
> flipping the allowlist. Remove your email (`dotnet user-secrets remove "Velocity:FullAccessEmails:0"`
> in `src/Velocity.Mcp.Server`), restart the server, reconnect — the currency tool disappears for
> you. Put it back afterward.

---

## 7. The Skill (1 min, optional)

```bash
npx skills add ovunc-gursoy/velocity_poc -s velocity
```

- **Say:** "The fourth surface is pure documentation — it teaches an agent *when* to reach for
  these tools and the traps in the data. Same source repo, no code."

---

## 8. Close — and be honest about scope (1 min)

> "Everything you saw is one core library behind four surfaces, exactly as the architecture
> intended. It's a proof of concept: the tools are real and the auth flow is real, but a couple
> of things are deliberately deferred before this would go live — notably tightening token
> audience binding, and hosting it behind HTTPS. All of that is written down."

Point at [`docs/decisions.md`](docs/decisions.md) and [`docs/architecture.md`](docs/architecture.md).
Being upfront about the PoC's edges builds more trust than pretending it's production.

---

## Appendix — reset & recover

| Situation | Fix |
|---|---|
| Server won't start: `Clerk:Authority is not configured` | Set the user-secret (step 0). By design it refuses to run open. |
| Currency tool missing for the *privileged* user | Check the email in `Velocity:FullAccessEmails` matches the signed-in account exactly; restart the server after changing secrets. |
| Agent won't re-trigger OAuth | Disconnect/reconnect the server in the client, or restart the client session. |
| `.mcp.json` got messy | `velocity mcp remove`, or edit the file — it only owns its own entry. |
| Need to re-run the self-check | `dotnet run --project tests/Velocity.Mcp.Core.SelfCheck` (needs network). |
