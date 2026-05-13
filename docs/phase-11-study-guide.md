# Phase 11 Study Guide: Dashboard

You turned a three-number placeholder into a real ops dashboard: stat cards with meaningful filtered counts, visual severity/status breakdowns with CSS bars, a recent activity feed, and a live alert banner for urgent incidents.

---

## The Big Picture

```
Dashboard sections (top to bottom):

1. [Alert Banner]       ← only renders when Critical/High incidents are open
2. [Stat Cards]         ← open incidents, total runbooks, knowledge count
3. [Breakdown Bars]     ← incident counts by status + severity, side by side
4. [Recent Activity]    ← last 5 incidents + last 5 runbooks, two columns
```

All data comes from two lists loaded once in `OnInitializedAsync` — no extra database queries for any of the computed values.

---

## Group 1: Load Real Data + LINQ Stats

### 🟢 Simple
Previously the dashboard loaded all incidents just to call `.Count` and throw the data away. Now you keep the full lists and compute every stat you need from them — one trip to the database, many different counts.

### 🟡 Real — `.Count(predicate)`

`.Count(predicate)` on a `List<T>` counts only items where the condition is true:

```csharp
_incidents.Count(i => i.Status == IncidentStatus.Open)
// → how many incidents have status Open?

_runbooks.Count(r => r.Status == RunbookStatus.Active)
// → how many runbooks are Active?
```

This is a LINQ **in-memory** operation — the whole list is already loaded, you're just filtering it with a lambda. No extra database call needed.

### Loading data once, using it many times
```csharp
protected override async Task OnInitializedAsync()
{
    _incidents = await IncidentService.GetAllAsync();   // load once
    _runbooks  = await RunbookService.GetAllAsync();    // load once
    _knowledgeCount = (await KnowledgeService.GetAllAsync()).Count;
}
```

`_incidents` and `_runbooks` are full `List<T>` — every computation in the template reads from these lists without going back to the database.

### Meaningful stat card values
| Before | After | Why better |
|--------|-------|-----------|
| Total incidents | Open + Investigating count | Closed/resolved incidents aren't actionable |
| Total runbooks | Total runbooks | (edit UI doesn't exist yet to set Active) |
| Total knowledge | Total knowledge | Count is the right metric here |

---

## Group 2: CSS Progress Bars

### 🟢 Simple
A number like "7 incidents" is less useful than seeing that 5 of them are Critical. A horizontal bar makes the proportion instantly readable — your eye processes width before your brain reads a label. No JS chart library needed — just two nested divs.

### 🟡 Real — Two New Concepts

**1. Razor `@{ }` blocks in the template**

You can declare C# variables inside the markup using a code block:

```razor
@{
    var total        = _incidents.Count > 0 ? _incidents.Count : 1;
    var openCount    = _incidents.Count(i => i.Status == IncidentStatus.Open);
    var criticalCount = _incidents.Count(i => i.Severity == IncidentSeverity.Critical);
}
```

These variables are computed once and reused throughout the template below. The `total > 0 ? total : 1` guard prevents divide-by-zero when there are no incidents.

**2. Inline `style=` with dynamic values**

Tailwind handles static styles. For values that change with data (like a percentage), use an inline style attribute:

```razor
<div class="bg-ctp-red h-1.5 rounded-full" style="width: @(openCount * 100 / total)%"></div>
```

`@(...)` evaluates the C# expression. The `%` is just a string appended after the number. Integer division is fine here — bar widths don't need sub-pixel precision.

### The bar pattern
Every bar is two divs: a full-width grey track and a colored fill inside it:

```razor
<div class="w-full bg-ctp-surface rounded-full h-1.5">          ← grey track
    <div class="bg-ctp-red h-1.5 rounded-full"
         style="width: @(count * 100 / total)%"></div>           ← colored fill
</div>
```

`rounded-full` on both makes the track and fill into pill shapes. `h-1.5` is 6px — thin enough to be subtle but visible.

### Color mapping used
| Status | Color | Severity | Color |
|--------|-------|----------|-------|
| Open | `bg-ctp-red` | Critical | `bg-ctp-red` |
| Investigating | `bg-ctp-yellow` | High | `bg-ctp-peach` |
| Resolved | `bg-ctp-green` | Medium | `bg-ctp-yellow` |
| Closed | `bg-ctp-overlay` | Low | `bg-ctp-green` |

---

## Group 3: Recent Activity Feed

### 🟢 Simple
A list of the 5 most recent incidents and runbooks — sorted newest first, showing just enough to understand what happened and when. Long titles truncate with `…` rather than wrapping and breaking the layout.

### 🟡 Real

**`.OrderByDescending().Take(5).ToList()` — the "latest N" pattern**

```csharp
var recentIncidents = _incidents
    .OrderByDescending(i => i.CreatedAt)
    .Take(5)
    .ToList();
```

- `.OrderByDescending(i => i.CreatedAt)` — sort newest first
- `.Take(5)` — keep only the first 5
- `.ToList()` — materialize into a concrete list (needed because you'll iterate it in the template)

For runbooks, sort by `UpdatedAt` instead of `CreatedAt` — a runbook edited recently is more relevant than one created recently and never touched:

```csharp
var recentRunbooks = _runbooks.OrderByDescending(r => r.UpdatedAt).Take(5).ToList();
```

**`truncate` — Tailwind text overflow**

```razor
<p class="text-ctp-text text-xs font-medium truncate">@incident.Title</p>
```

`truncate` applies `overflow: hidden; text-overflow: ellipsis; white-space: nowrap` — long text gets cut off with `…` at the container boundary. Requires the parent to have a defined width (use `min-w-0` on flex children to enable this).

**Why `min-w-0` matters:**
Flex children don't shrink below their content size by default. Without `min-w-0`, a long title would push the layout wider instead of truncating. `min-w-0` tells the flex child "you can shrink to zero, let the text truncate."

```razor
<div class="flex-1 min-w-0">
    <p class="text-ctp-text text-xs font-medium truncate">@incident.Title</p>
</div>
```

**Declaring variables inside `@{ }` in loops:**
```razor
@{
    var recentIncidents = _incidents.OrderByDescending(i => i.CreatedAt).Take(5).ToList();
}
@foreach (var incident in recentIncidents) { ... }
```

Declaring inside a `@{ }` block before the `@foreach` is cleaner than computing inside the loop. The variable is scoped to the current template block.

---

## Group 4: Alert Banner

### 🟢 Simple
A smoke alarm only goes off when there's smoke. The alert banner only renders when there are Critical or High incidents that are still open. If everything is resolved, the banner doesn't exist in the DOM — it's not hidden, it's simply not there.

### 🟡 Real — Chained LINQ Conditions

`.Where()` with multiple `&&` and `||` conditions:

```csharp
var urgentIncidents = _incidents
    .Where(i =>
        (i.Status == IncidentStatus.Open || i.Status == IncidentStatus.Investigating) &&
        (i.Severity == IncidentSeverity.Critical || i.Severity == IncidentSeverity.High))
    .OrderByDescending(i => i.Severity)
    .ToList();
```

Read as: "incidents where status is active (open or investigating) AND severity is serious (critical or high)." The parentheses group the OR conditions so `&&` applies between the two groups.

**Why `.Any()` instead of `.Count > 0`**

```csharp
@if (urgentIncidents.Any())
```

`.Any()` returns `true` as soon as it finds one matching item — it stops looking. `.Count > 0` counts all items first. For "does at least one exist?" questions, `.Any()` is the right tool.

**Sorting by enum value:**
```csharp
.OrderByDescending(i => i.Severity)
```

Your `IncidentSeverity` enum is defined as `Low=0, Medium=1, High=2, Critical=3`. Ordering descending by the enum puts Critical (3) before High (2) automatically — no extra logic needed.

**`@if` = conditional DOM existence (not just visibility)**
```razor
@if (urgentIncidents.Any())
{
    <div class="border border-ctp-red ...">...</div>
}
```

When the condition is false, the `<div>` doesn't exist in the HTML at all — different from CSS `display: none` which hides it but leaves it in the DOM. This is more correct: the alert truly isn't there when everything is fine.

### Banner position
The alert banner goes **before the stat cards** — at the very top of the dashboard content. When there's an active alert, it's the first thing you see.

---

## Troubleshooting

### Bars don't show (width stays 0)
Check that `total` is at least 1. The guard `_incidents.Count > 0 ? _incidents.Count : 1` prevents division that results in 0. If `_incidents` is empty, `total = 1` and all counts are 0, so all bars show 0% width — correct behavior.

### Bar widths don't add up to 100%
Integer division truncates — `3 * 100 / 7 = 42` not `42.86`. The bars won't always sum to exactly 100% visually. This is acceptable for a dashboard bar — it's not a pie chart. If you want precision, use `(int)Math.Round(count * 100.0 / total)`.

### Alert banner shows for resolved incidents
Double-check the status condition includes both `Open` AND `Investigating`:
```csharp
i.Status == IncidentStatus.Open || i.Status == IncidentStatus.Investigating
```
`Resolved` and `Closed` are deliberately excluded.

### Recent list shows old items at the top
Confirm `.OrderByDescending` not `.OrderBy` — ascending order puts the oldest first.

### Long title doesn't truncate
The flex parent needs `min-w-0` and the text element needs `truncate`:
```razor
<div class="flex-1 min-w-0">
    <p class="truncate">long title here</p>
</div>
```
Without `min-w-0` on the flex child, the container expands to fit the text instead of clipping it.

### Open Incidents stat card shows 0
Your incidents might all be in Resolved or Closed status. Create a new incident (default is Open) to verify the count updates. The stat card filters to `Open || Investigating` only.

---

## Phase 11 Concepts at a Glance

| Concept | Where Used |
|---------|-----------|
| `.Count(predicate)` | Count items matching a condition from an in-memory list |
| Load once, compute many | One `GetAllAsync()` per service, multiple stats derived from it |
| `@{ var x = ...; }` in template | Declare computed variables inline in Razor markup |
| `style="width: @(expr)%"` | Dynamic CSS values that Tailwind classes can't handle |
| Bar pattern (track + fill) | Two nested divs — outer grey, inner colored with dynamic width |
| `.OrderByDescending().Take(5).ToList()` | "Latest N items" pattern |
| `UpdatedAt` for recency | More relevant than `CreatedAt` for frequently-edited records |
| `truncate` + `min-w-0` | Truncate long text in flex layouts without breaking layout |
| Chained `.Where()` with `&&` / `||` | Filter by multiple conditions simultaneously |
| `.Any()` | "Does at least one match?" — stops at first hit, more efficient than `.Count > 0` |
| Enum sort order | `OrderByDescending(i => i.Severity)` works because enums have numeric values |
| `@if` = DOM existence | Element doesn't render at all when false — not just hidden |

---

## What the Dashboard Now Shows

```
┌─────────────────────────────────────────────┐
│ ⚠ Active Alerts (if any Critical/High open) │  ← red border, only when urgent
├──────────────┬──────────────┬───────────────┤
│ Open         │ Total        │ Knowledge     │  ← stat cards
│ Incidents    │ Runbooks     │ Entries       │
├──────────────┴──────────────┴───────────────┤
│ Incidents by Status │ Incidents by Severity │  ← CSS bar breakdowns
│ ████░░░░ Open 3     │ ██░░░░░░ Critical 1  │
│ █░░░░░░░ Invst. 1   │ ████░░░░ High 3      │
│ ...                 │ ...                  │
├─────────────────────┬───────────────────────┤
│ Recent Incidents    │ Recent Runbooks       │  ← latest 5 of each
│ DB crash    Critical│ DB Restart   Draft   │
│ API timeout High    │ Auth flow    Draft   │
└─────────────────────┴───────────────────────┘
```

---

*Phase 11 complete. Sentinel is now feature-complete as a learning project.*
*Phase 12 — Self-hosted deployment: Docker Compose, production config, team access via IP address*
*Phase 13 — Azure deployment: App Service, managed PostgreSQL, GitHub Actions CI/CD*
