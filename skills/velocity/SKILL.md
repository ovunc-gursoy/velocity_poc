---
name: velocity
description: Look up FIFA World Cup tournament results (1930-2026) and convert currency at European Central Bank reference rates. Use when asked who won a World Cup, where one was held, final scores, hosts or attendance; or to convert an amount between currencies, today or on a past date. Also covers installing the Velocity MCP server if its tools are not available.
---

# Velocity

Two tools, backed by one shared core library. Same code answers whether you reach it over
remote MCP, local MCP, or the `velocity` CLI — the answers do not vary by surface.

## Are the tools available?

Check your tool list for `get_world_cup_tournaments` and `convert_currency`.

If they are missing, the local MCP server registers itself in one command:

```
velocity mcp install     # writes/merges the .mcp.json entry, then restart the MCP client
```

`velocity mcp remove` reverses it. Both leave any other servers in `.mcp.json` untouched.
If the `velocity` command itself is missing, the CLI is a dotnet tool — install that first.

Without the tools you have no World Cup data and no rates. Say so rather than answering
from memory: the point of these tools is that their answers are checkable and current.

## get_world_cup_tournaments

Every men's FIFA World Cup from 1930 to 2026: host, winner, runner-up, final score, venue,
city, attendance, and notes. All filters are optional and combine with AND; no filters lists
everything.

| Filter | Meaning |
|---|---|
| `year` | Exact tournament year, e.g. `1966`. |
| `team` | A team that *reached the final*, as winner or runner-up. Case-insensitive, matches the full name. |
| `host` | Host nation. Substring match. |

**Team names are historical, not current.** Use `West Germany` for 1954-1990 and `Germany`
from 1994. If someone asks how many World Cups Germany has won, you need *both* queries: the
answer is four, but only 2014 is filed under `Germany` — 1954, 1974 and 1990 are under
`West Germany`. Query one name and you will be three titles short. The same applies to
`Czechoslovakia`, which no longer exists but reached two finals.

**A team filter only finds finalists.** A team that went out in the semi-final does not appear.
An empty result means "did not reach a final", not "did not compete".

**2026 has no winner.** The tournament is hosted by the United States, Canada and Mexico, and
the final is scheduled for 19 July 2026. `winner`, `runner_up` and `score` are null. Report it
as not yet played — do not guess, and do not read null as an error.

**1942 and 1946 return nothing.** No tournament was held; the World Cup was suspended for the
Second World War. An empty result is the correct answer, not a failure.

**1950 is a special case.** It has a winner (Uruguay), but was decided by a final-round group
match rather than a one-off final. The `notes` field says so — pass that on if the score matters.

Attendance figures for the earliest finals vary between sources. Treat them as approximate.

## convert_currency

Converts an amount between currencies. Takes `amount`, `from`, `to`, and an optional `date`.

```
convert_currency(amount: 100, from: "USD", to: "EUR")
convert_currency(amount: 100, from: "USD", to: "EUR", date: "2024-01-15")
```

Returns the converted amount, the rate used, and `rateDate` — the date the rate was actually
published.

**These are ECB reference rates, not live trading rates.** They are published once per working
day. Do not present a result as a real-time or dealable rate, and do not use it to quote a
trade. For an approximate everyday conversion it is fine.

**`rateDate` is the authority, and it may not be the date asked for.** Weekends and holidays
roll back to the previous working day: ask for Sunday 2024-01-14 and you get Friday 2024-01-12's
rate. Read `rateDate` and say which date the rate is from when it differs from the request.

**Coverage is 30 major currencies** — the ones the ECB publishes. No cryptocurrency, no
precious metals, and many national currencies are absent. Rates begin at **1999-01-04**, the
euro's first working day; earlier dates fail rather than rolling forward, and future dates are
rejected.

Codes are 3-letter ISO 4217 (`USD`, `EUR`, `JPY`), case-insensitive. Converting a currency to
itself returns the amount unchanged at rate 1.

## When a call fails

Error messages are written to be read and acted on — they name the bad argument and the
expected format. Read the message and retry rather than reporting a bare failure to the user.

An empty result is not an error. `get_world_cup_tournaments` returns nothing for a wartime gap
year or a team that never reached a final, and that *is* the answer.
