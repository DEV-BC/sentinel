# Phase 7 Study Guide: Incidents Page

You built a full Incidents feature — list, create, color badges, and a conditional action. Here's everything you learned, explained two ways.

---

## Group 1: Page Scaffold & DateTime Formatting

### 🟢 Simple
You already knew how to build a list page from Runbooks. This was the same blueprint: load data on init, show it in a loop, toggle a form. The new thing was displaying a date in a readable format.

### 🟡 Real
`DateTime.ToString("MMM d, yyyy")` uses a **format string** — a pattern that tells C# how to render the date. `MMM` = short month name (May), `d` = day number, `yyyy` = 4-digit year. Format strings are built into C#'s `DateTime` type.

```razor
<p class="text-ctp-muted text-xs mt-1">@incident.CreatedAt.ToString("MMM d, yyyy")</p>
```

Common format tokens:
| Token | Output |
|-------|--------|
| `MMM` | May |
| `MMMM` | May (full) |
| `d` | 12 |
| `dd` | 12 (zero-padded) |
| `yyyy` | 2026 |
| `HH:mm` | 14:30 |

### Key Pattern
```razor
@rendermode InteractiveServer   ← needed for button clicks
@attribute [Authorize]          ← blocks unauthenticated users
@inject IIncidentService IncidentService

protected override async Task OnInitializedAsync()
{
    _incidents = await IncidentService.GetAllAsync();
}
```

---

## Group 2: Enum Dropdowns with `InputSelect<TEnum>`

### 🟢 Simple
A dropdown forces the user to pick from valid options — no typos, no invalid values. `InputSelect` is Blazor's built-in dropdown component that binds directly to an enum property.

### 🟡 Real
`InputSelect<TEnum>` is a **generic component** — the `<TEnum>` tells it which type to bind to. Inside, `Enum.GetValues<T>()` returns every value of that enum automatically. No need to hardcode the options — if you add a new severity level to the enum, the dropdown updates for free.

```razor
<InputSelect @bind-Value="_form.Severity"
             class="w-full bg-ctp-surface border border-ctp-overlay rounded px-3 py-2 text-ctp-text text-sm">
    @foreach (var severity in Enum.GetValues<IncidentSeverity>())
    {
        <option value="@severity">@severity</option>
    }
</InputSelect>
```

### Why not just use a plain `<select>`?
A plain HTML `<select>` returns a string. `InputSelect<TEnum>` handles the conversion from the selected string back to the typed enum value, so your form model property stays strongly typed.

### Form Model Pattern (same as Runbooks)
```csharp
private sealed class CreateIncidentForm
{
    [Required] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;  // default
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;          // default
}
```

The defaults (`= IncidentSeverity.Medium`) pre-select that option in the dropdown when the form opens.

---

## Group 3: Color Badges with Switch Expressions

### 🟢 Simple
Color communicates severity before you read a word. Red means bad, green means good — this is what makes ops dashboards scannable at a glance rather than requiring careful reading.

### 🟡 Real
You wrote two `static` helper methods that take an enum value and return a CSS class string. They're `static` because they don't touch any component state — they're pure functions: same input always gives the same output.

```csharp
private static string SeverityClass(IncidentSeverity severity) => severity switch
{
    IncidentSeverity.Critical => "bg-ctp-red text-ctp-base",
    IncidentSeverity.High     => "bg-ctp-peach text-ctp-base",
    IncidentSeverity.Medium   => "bg-ctp-yellow text-ctp-base",
    IncidentSeverity.Low      => "bg-ctp-green text-ctp-base",
    _                         => "bg-ctp-surface text-ctp-subtle",
};

private static string StatusClass(IncidentStatus status) => status switch
{
    IncidentStatus.Open          => "bg-ctp-red text-ctp-base",
    IncidentStatus.Investigating => "bg-ctp-yellow text-ctp-base",
    IncidentStatus.Resolved      => "bg-ctp-green text-ctp-base",
    IncidentStatus.Closed        => "bg-ctp-surface text-ctp-muted",
    _                            => "bg-ctp-surface text-ctp-subtle",
};
```

Called inline in the template:
```razor
<span class="text-xs px-2 py-1 rounded @SeverityClass(incident.Severity)">@incident.Severity</span>
<span class="text-xs px-2 py-1 rounded @StatusClass(incident.Status)">@incident.Status</span>
```

### Catppuccin Mocha severity color map
| Severity | Background | Meaning |
|----------|-----------|---------|
| Critical | `ctp-red` | Fire |
| High | `ctp-peach` | Urgent |
| Medium | `ctp-yellow` | Watch |
| Low | `ctp-green` | Minor |

---

## Group 4: Mark as Resolved — Conditional Buttons & Nullable DateTime

### 🟢 Simple
Some actions only make sense in certain states. A "Mark as Resolved" button on an already-resolved incident is confusing. You used `@if` inside the card to show the button only when the incident is still open.

### 🟡 Real — Two New Concepts

**1. Nullable DateTime (`DateTime?`) with `.HasValue` and `.Value`**

`ResolvedAt` is `DateTime?` — it might or might not have a date. You can't call `.ToString()` directly on a nullable — you have to check first:

```razor
@if (incident.ResolvedAt.HasValue)
{
    <p class="text-ctp-green text-xs">Resolved @incident.ResolvedAt.Value.ToString("MMM d, yyyy")</p>
}
```

- `.HasValue` → `true` if there's a date, `false` if null
- `.Value` → unwraps and gives you the actual `DateTime` (only safe after checking `.HasValue`)

You can also write this as: `incident.ResolvedAt?.ToString(...)` — the `?.` null-conditional operator does both steps in one.

**2. `FirstOrDefault` — LINQ search**

```csharp
var incident = _incidents.FirstOrDefault(i => i.Id == id);
if (incident is null) return;
```

`FirstOrDefault` searches a list and returns the first item where the condition is true, or `null` if nothing matches. The `i => i.Id == id` is a lambda — a small inline function that runs for each item in the list.

**3. Conditional button**

```razor
@if (incident.Status == IncidentStatus.Open || incident.Status == IncidentStatus.Investigating)
{
    <button @onclick="() => MarkResolved(incident.Id)"
            class="text-xs text-ctp-green hover:underline">
        Mark as Resolved
    </button>
}
```

Once resolved or closed, this button simply doesn't exist in the DOM.

### Full handler
```csharp
private async Task MarkResolved(Guid id)
{
    var incident = _incidents.FirstOrDefault(i => i.Id == id);
    if (incident is null) return;

    incident.Status = IncidentStatus.Resolved;
    incident.ResolvedAt = DateTime.UtcNow;
    await IncidentService.UpdateAsync(incident);
    _incidents = await IncidentService.GetAllAsync();
}
```

---

## What You Built: The Full Incidents Feature

```
[Browser] clicks "New Incident"
    → _showForm = true → form appears
    → user fills Title, picks Severity/Status from dropdowns
    → clicks Create → HandleCreate() fires
    → new Incident{} sent to IncidentService.CreateAsync()
    → IncidentService calls _db.Incidents.Add() + SaveChangesAsync()
    → data written to PostgreSQL
    → _incidents reloaded → list updates

[Browser] clicks "Mark as Resolved"
    → MarkResolved(id) fires
    → FirstOrDefault finds the incident in the list
    → Status = Resolved, ResolvedAt = now
    → IncidentService.UpdateAsync() saves changes
    → _incidents reloaded → badge turns green, button disappears
```

---

## Troubleshooting

### Dropdown shows numbers instead of names (e.g., "0", "1", "2")
The `<option value="@severity">@severity</option>` renders enum values as their string names. If you're seeing numbers, check that both the `value` and display are `@severity` (not `@((int)severity)`).

### "Mark as Resolved" button not disappearing after click
Make sure you're calling `_incidents = await IncidentService.GetAllAsync()` at the end of `MarkResolved` — this reloads the list from the database so the UI reflects the new state.

### `ResolvedAt` shows the wrong date or time
`DateTime.UtcNow` stores UTC time. If you're displaying it and it looks off by hours, that's the timezone difference. For now UTC is fine — timezone conversion is a later concern.

### `incident.ResolvedAt.Value.ToString()` throws a NullReferenceException
This happens if you call `.Value` without checking `.HasValue` first. Always gate it with `@if (incident.ResolvedAt.HasValue)` in the template, or use the null-conditional operator: `incident.ResolvedAt?.ToString("MMM d, yyyy")`.

### `FirstOrDefault` returns null even though the incident exists
Check that you're comparing the right property. `i => i.Id == id` requires both to be `Guid` type. If `id` is a string, the types won't match and no item will be found.

---

## Phase 7 Concepts at a Glance

| Concept | Where Used |
|---------|-----------|
| `DateTime.ToString("MMM d, yyyy")` | Incident card — created date |
| `InputSelect<TEnum>` | Create form — severity and status dropdowns |
| `Enum.GetValues<T>()` | Populating dropdown options automatically |
| `static` helper methods | `SeverityClass()`, `StatusClass()` |
| Switch expression → CSS class | Color badges |
| `DateTime?` with `.HasValue` / `.Value` | Resolved date display |
| `FirstOrDefault(i => i.Id == id)` | Finding an incident to update |
| Conditional `@if` on a button | "Mark as Resolved" only when actionable |

---

*Phase 7 complete. Next up: Phase 8 — AI Integration (semantic search with pgvector and the OpenAI embedding API).*