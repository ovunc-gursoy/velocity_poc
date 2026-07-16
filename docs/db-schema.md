# Database schema

> Current schema of record. Update in the same change as any migration.

## Engine
- DB / version: SQLite via `Microsoft.Data.Sqlite` 10.0.10. In-memory, shared cache (`Data Source=worldcup;Mode=Memory;Cache=Shared`).
- Migration tool: none. The schema is created and seeded from `src/Velocity.Mcp.Core/worldcup.sql` (embedded resource) on each `WorldCupDb` construction. There is no persisted database to migrate.

## Tables

### tournaments — one row per FIFA World Cup, 1930-2026

| Column | Type | Notes |
|---|---|---|
| `year` | INTEGER | PK. Every 4 years from 1930; **no rows for 1942 or 1946** (WWII). |
| `hosts` | TEXT | NOT NULL. Co-hosts joined with `/`, e.g. `South Korea / Japan`. Queried with LIKE substring match. |
| `winner` | TEXT | **NULL when the final has not been played** — currently 2026. |
| `runner_up` | TEXT | NULL alongside `winner`. |
| `score` | TEXT | Final score with context, e.g. `0-0 (3-2 pens)`, `4-2 (a.e.t.)`. |
| `venue` | TEXT | Stadium name. |
| `city` | TEXT | |
| `attendance` | INTEGER | NULL if unplayed. Figures for early finals vary between sources. |
| `notes` | TEXT | NULL for most rows. Context an agent would otherwise get wrong. |

## Relationships
- None. Single table.

## Gotchas
- Team names are historical, not current: `West Germany` for 1954-1990, `Germany` from 1994. A `team` filter is exact (case-insensitive), so `Germany` will not return the West Germany finals.
- 1950 has a `winner` but was decided by a final-round group match rather than a one-off final; `notes` says so.

## Migrations log
| Date | Id | Summary |
|---|---|---|
| 2026-07-16 | initial | Create `tournaments`, seed 23 rows (1930-2026). |
