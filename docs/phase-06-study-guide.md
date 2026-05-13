# Phase 6 Study Guide — Application Layer: Services

## 📖 Concepts Learned

### What is the Application Layer?

🟢 **Simple:** Think of a restaurant. The **menu** lists everything you can order — get all runbooks, create an incident, search knowledge base entries. The **kitchen** knows how to prepare each dish using EF Core and the database. The **dining room** (Blazor pages) lets users place orders. The dining room never walks into the kitchen directly — it always orders through the menu. The Application layer is the menu.

🟡 **Real:** The Application layer (`Sentinel.Application`) sits between Domain and Infrastructure. It defines **what** the system can do via interfaces, without saying **how**. The Infrastructure layer provides the how (implementations). The Web layer calls the what (interfaces). This means:
- You can swap PostgreSQL for SQLite by changing Infrastructure — Web never changes
- You can add caching to `RunbookService` without touching any Blazor component
- You can unit-test Blazor pages by swapping real services for fake ones

```
Domain  ←──  Application (interfaces)  ←──  Infrastructure (implementations)
                      ↑                               ↑
                      └──────────── Web ──────────────┘
                                (composition root)
```

---

### C# Interfaces

🟢 **Simple:** A menu item says "Grilled Salmon" — it doesn't explain the recipe. An interface is that menu item. It lists method names, what goes in, and what comes out. No logic, no code body. Any class that signs the contract must provide all the methods exactly as described.

🟡 **Real:** The `interface` keyword declares a contract — a list of method signatures with no implementations:

```csharp
public interface IRunbookService
{
    Task<List<Runbook>> GetAllAsync();
    Task<Runbook?> GetByIdAsync(Guid id);
    Task CreateAsync(Runbook runbook);
    Task UpdateAsync(Runbook runbook);
    Task DeleteAsync(Guid id);
}
```

Rules:
- No method bodies — only signatures
- No fields or constructor logic
- Any class implementing it must provide **all** methods or the compiler errors
- A class can implement multiple interfaces (unlike inheritance, which is single-parent only)

**Why `Task<T>` everywhere?** All database operations are async — they wait for the database to respond. `Task<List<Runbook>>` means "this runs asynchronously and eventually returns a list of runbooks." `Task` alone (no `<T>`) means "runs asynchronously, returns nothing."

**Why `Runbook?` on `GetByIdAsync`?** The ID might not exist in the database. The `?` signals that "not found" is a valid outcome — not an error.

---

### Implementing an Interface

🟢 **Simple:** The kitchen signs the menu contract. Now it must actually prepare every dish listed. `: IRunbookService` on a class is how it signs the contract. If the kitchen forgets to prepare one dish, the health inspector (compiler) shuts it down immediately.

🟡 **Real:** `: IRunbookService` on a class means "I implement this interface." The compiler verifies every method is present with the exact matching signature:

```csharp
public class RunbookService : IRunbookService
{
    private readonly AppDbContext _db;

    public RunbookService(AppDbContext db) => _db = db;

    public async Task<List<Runbook>> GetAllAsync() =>
        await _db.Runbooks.OrderByDescending(r => r.CreatedAt).ToListAsync();

    // ... all other methods must be present
}
```

The service receives `AppDbContext` through its **constructor** — the DI container provides it automatically. You never write `new RunbookService(...)` yourself.

**Key EF Core methods used:**

| Method | SQL equivalent | Returns |
|--------|---------------|---------|
| `ToListAsync()` | `SELECT * FROM ...` | `List<T>` |
| `OrderByDescending(x => x.Prop)` | `ORDER BY Prop DESC` | queryable (chain more) |
| `FindAsync(id)` | `SELECT WHERE Id = @id` | `T?` (null if not found) |
| `Add(entity)` | stages `INSERT` | nothing yet |
| `Remove(entity)` | stages `DELETE` | nothing yet |
| `Update(entity)` | stages `UPDATE` | nothing yet |
| `SaveChangesAsync()` | executes all staged SQL | rows affected |

`Add`, `Remove`, and `Update` mark changes in EF Core's change tracker. `SaveChangesAsync()` is the moment SQL actually runs. Nothing touches the database until you call it.

---

### `UpdatedAt` as a Business Rule

🟢 **Simple:** Every time a runbook changes, the timestamp updates automatically. The UI doesn't have to remember — the service enforces it. Business rules live in the service layer, not scattered across UI components.

🟡 **Real:** In `UpdateAsync`, the service sets `UpdatedAt` before saving:

```csharp
public async Task UpdateAsync(Runbook runbook)
{
    runbook.UpdatedAt = DateTime.UtcNow;
    _db.Runbooks.Update(runbook);
    await _db.SaveChangesAsync();
}
```

If this lived in the UI instead, every page that updates a runbook would have to remember to set it. One page forgets → stale timestamps. Centralizing it in the service makes it impossible to miss.

---

### DI Registration — `AddScoped<TInterface, TImplementation>()`

🟢 **Simple:** The building's front desk (DI container) needs to know: when someone asks for "the runbook service," which kitchen fulfills that request? `AddScoped<IRunbookService, RunbookService>()` is how you tell it: "for `IRunbookService` requests, hand them a `RunbookService`."

🟡 **Real:** Three lifetimes exist in ASP.NET Core DI:

| Lifetime | How long it lives | Use for |
|----------|------------------|---------|
| `AddSingleton` | Entire app lifetime | Config, caches, stateless services |
| `AddScoped` | One per HTTP request / Blazor circuit | DbContext, services that use DbContext |
| `AddTransient` | New instance every time | Lightweight stateless utilities |

We use `AddScoped` because our services use `AppDbContext`, which is also scoped. If a service is scoped, everything it depends on must also be scoped or singleton — never transient.

```csharp
// Program.cs — wiring interface to implementation
builder.Services.AddScoped<IRunbookService, RunbookService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IKnowledgeService, KnowledgeService>();
```

After this, `@inject IRunbookService RunbookService` in any component gets a `RunbookService` automatically — the component never knows the concrete type.

---

### Separating Form Models from Domain Entities

🟢 **Simple:** The order form at a restaurant has fields for "table number" and "special requests." The kitchen receipt has fields like "ticket #", "timestamp", and "station." They represent the same meal but from different perspectives. The form is what the customer fills out; the domain entity is what the kitchen tracks.

🟡 **Real:** `Runbook` is a domain entity — it has `Id`, `CreatedAt`, `UpdatedAt`, `Status`, and C# `required` keyword constraints. A create form only needs `Title`, `Content`, and `Author`. Mixing them causes problems:
- `Id` and `CreatedAt` shouldn't be in the form (the server generates them)
- `Status` starts as `Draft` automatically (not user input)
- `[Required]` validation attributes mix poorly with domain `required` keyword

The fix: a small inner class `CreateRunbookForm` holds only what the user types. `HandleCreate` maps it to the domain entity:

```csharp
private sealed class CreateRunbookForm
{
    [Required] public string Title   { get; set; } = string.Empty;
    [Required] public string Content { get; set; } = string.Empty;
    public string Author             { get; set; } = string.Empty;
}

private async Task HandleCreate()
{
    await RunbookService.CreateAsync(new Runbook
    {
        Title   = form.Title,
        Content = form.Content,
        Author  = form.Author
        // Status defaults to Draft, Id/CreatedAt generated automatically
    });
}
```

`sealed` on the inner class means it can't be extended — a hint to future readers that this class is internal to the component and not designed for reuse.

---

### `@foreach` in Razor Templates

🟢 **Simple:** A waiter reads the table's order list and recites each item. `@foreach` does the same — for every runbook in the list, stamp out one card in the HTML.

🟡 **Real:** `@foreach` in Razor works like C# `foreach`, but the body is HTML/Razor:

```razor
@foreach (var runbook in runbooks)
{
    <div class="...">
        <p>@runbook.Title</p>
        <span>@runbook.Status</span>
    </div>
}
```

When `runbooks` is empty, nothing renders. Pair with an `@if` for an empty state:

```razor
@if (runbooks.Count == 0)
{
    <p class="text-ctp-muted">No runbooks yet.</p>
}
else
{
    @foreach (var runbook in runbooks)
    { ... }
}
```

---

### `@if` Toggle for Conditional UI

🟢 **Simple:** A light switch. One bool, one button click, one panel appears or disappears — no page reload needed.

🟡 **Real:** A C# `bool` field in `@code` drives conditional rendering:

```csharp
private bool showForm;
```

```razor
<button @onclick="() => showForm = !showForm">
    @(showForm ? "Cancel" : "New Runbook")
</button>

@if (showForm)
{
    <div>... the form ...</div>
}
```

`!showForm` flips the bool. Blazor re-renders the component automatically when state changes — no manual DOM updates needed. `@(showForm ? "Cancel" : "New Runbook")` is a C# ternary inside Razor — reads as "if showForm is true, show Cancel; otherwise show New Runbook."

---

### `switch` Expressions for Status Colors

🟢 **Simple:** A colour-coded filing system. Draft files get yellow tabs, Active files get green tabs, Deprecated files get grey tabs. One rule per status, no repetition.

🟡 **Real:** The `switch` expression (C# 8+) returns a value based on a pattern:

```csharp
private static string StatusColor(RunbookStatus status) => status switch
{
    RunbookStatus.Active     => "bg-ctp-green text-ctp-base",
    RunbookStatus.Deprecated => "bg-ctp-overlay text-ctp-muted",
    _                        => "bg-ctp-yellow text-ctp-base",
};
```

`_` is the discard pattern — the catch-all for anything not explicitly listed (in our case, `Draft`). The compiler warns you if you forget a case (with enums), so this is exhaustive by design.

The method is `static` because it doesn't need any instance data — it only depends on its input. Static methods on components are a small performance win.

---

### `InputTextArea`

🟢 **Simple:** `InputText` renders a single-line text box. `InputTextArea` renders a multi-line text area — for longer content like runbook body text.

🟡 **Real:** `InputTextArea` is a Blazor built-in component in `Microsoft.AspNetCore.Components.Forms`. It works identically to `InputText` but renders as `<textarea>`. Additional HTML attributes like `rows` are passed through automatically:

```razor
<InputTextArea @bind-Value="form.Content" rows="4"
               class="... resize-none" />
```

`resize-none` is a Tailwind class that prevents the user from dragging the textarea to resize it — cleaner for a fixed-height content area.

---

## 🔧 Troubleshooting

### RZ10012: `AntiForgeryToken` component not found
**Cause:** Two issues: (1) wrong casing — it's `AntiforgeryToken` (lowercase 'f'), not `AntiForgeryToken`. (2) The namespace `Microsoft.AspNetCore.Components.Forms` wasn't imported.

**Fix:**
```razor
@* In _Imports.razor *@
@using Microsoft.AspNetCore.Components.Forms
```
```razor
@* In the form *@
<AntiforgeryToken />   @* lowercase 'f' *@
```

---

### BL0008: `[SupplyParameterFromForm]` property has an initializer
**Cause:** On form POST, the framework sets the property to `null` before populating it from form data. A property initializer (`= new()`) conflicts with this — the warning says it can be overwritten.

**Fix:** Remove the initializer, make the property nullable, and use `OnInitialized` with `??=` to guarantee a non-null value on GET requests:

```csharp
[SupplyParameterFromForm]
private LoginModel? _model { get; set; }

protected override void OnInitialized() => _model ??= new();
```

`??=` means: "assign `new()` only if `_model` is currently null." On GET, it initializes. On POST, `[SupplyParameterFromForm]` has already set it, so `??=` does nothing.

---

### CS8602: Dereference of a possibly null reference (in Razor bindings)
**Cause:** After making the form model nullable (to fix BL0008), the Razor compiler sees `@bind-Value="_model.Email"` and warns that `_model` could be null.

**Fix:** Use the null-forgiving operator `!` on bindings. Since `OnInitialized` guarantees non-null before the template renders, `!` is safe here:

```razor
<EditForm Model="_model!" ...>
<InputText @bind-Value="_model!.Email" ...>
<ValidationMessage For="() => _model!.Email" ...>
```

The `!` tells the compiler "I know this won't be null here — trust me." It does not add any runtime check; it only suppresses the compile-time warning.

---

### Dashboard counts show 0 after creating a runbook (if using old direct DbContext approach)
**Cause:** After switching to services, the old `Home.razor` still injected `AppDbContext` directly. The new service counts and the old direct counts are separate code paths.

**Fix:** Ensure `Home.razor` uses the services:
```razor
@inject IRunbookService RunbookService
@inject IIncidentService IncidentService
@inject IKnowledgeService KnowledgeService
```

---

## 🏗️ What We Built

### Files created or modified
| File | Action | Purpose |
|---|---|---|
| `src/Sentinel.Application/IRunbookService.cs` | Created | Menu item — what runbook operations exist |
| `src/Sentinel.Application/IIncidentService.cs` | Created | Menu item — what incident operations exist |
| `src/Sentinel.Application/IKnowledgeService.cs` | Created | Menu item — what knowledge operations exist |
| `src/Sentinel.Infrastructure/Services/RunbookService.cs` | Created | Kitchen — EF Core implementation |
| `src/Sentinel.Infrastructure/Services/IncidentService.cs` | Created | Kitchen — EF Core implementation |
| `src/Sentinel.Infrastructure/Services/KnowledgeService.cs` | Created | Kitchen — EF Core implementation |
| `src/Sentinel.Web/Program.cs` | Modified | Registered all three services with DI |
| `src/Sentinel.Web/Components/_Imports.razor` | Modified | Added `@using Sentinel.Application` and `@using Sentinel.Domain` |
| `src/Sentinel.Web/Components/Pages/Home.razor` | Modified | Replaced direct DbContext with service calls |
| `src/Sentinel.Web/Components/Pages/Runbooks.razor` | Modified | Full list + create page using `IRunbookService` |

### Architecture after Phase 6
```
Blazor Page
    ↓ @inject
IRunbookService  (Sentinel.Application — interface, no DB knowledge)
    ↓ implemented by
RunbookService   (Sentinel.Infrastructure — EF Core, knows the DB)
    ↓ uses
AppDbContext     (Sentinel.Infrastructure — EF Core DbContext)
    ↓ talks to
PostgreSQL       (external — database)
```

---

## 💡 Interview Q&A

**Q: What is the difference between an interface and a class in C#?**
A: A class is a blueprint that defines both structure (properties) and behavior (method implementations). An interface defines only the contract — method signatures with no bodies. A class can only inherit from one other class, but it can implement many interfaces. Interfaces enable programming to abstractions: your code depends on what something does, not how it does it.

**Q: What does `AddScoped` mean in ASP.NET Core DI and when should you use it?**
A: `AddScoped` means one instance per HTTP request (or per Blazor circuit for Blazor Server). Use it for services that hold per-request state or depend on scoped services like `DbContext`. If a service is scoped, all its dependencies must also be scoped or singleton — a scoped service cannot safely depend on a transient service that might have a different instance.

**Q: Why is `SaveChangesAsync()` separate from `Add()`, `Update()`, and `Remove()`?**
A: EF Core uses a Unit of Work pattern. `Add`, `Update`, and `Remove` register intent — they mark entities in the change tracker but don't touch the database. `SaveChangesAsync()` commits all pending changes in a single database transaction. This lets you make multiple changes atomically: either all succeed or all fail. It's also more efficient — one round-trip to the database instead of one per change.

**Q: Why have a separate form model (`CreateRunbookForm`) instead of using the domain entity directly?**
A: Domain entities have properties the user shouldn't set (Id, CreatedAt, UpdatedAt) and use the C# `required` keyword for compile-time safety rather than `[Required]` for runtime validation. Mixing UI validation attributes into domain entities couples the domain to the presentation layer. A form model contains exactly what the user inputs, with validation attributes appropriate for a form. The service method maps form data to the domain entity, keeping both layers clean.

**Q: What is the `_` discard pattern in a `switch` expression?**
A: `_` is the catch-all arm — it matches anything not explicitly listed above it. In a `switch` expression on an enum, the compiler warns if you haven't covered all values (exhaustiveness checking). Using `_` satisfies the compiler for any remaining cases and serves as a safe fallback. It's equivalent to `default:` in a traditional `switch` statement.

**Q: Why use `static` on a helper method in a Blazor component?**
A: A `static` method doesn't access any instance data — it only uses its parameters. In a Blazor component, marking a method `static` tells both the compiler and future readers that it has no side effects on component state. It's a slight performance improvement (no implicit `this` capture in delegates) and improves clarity: if a method is static, it's a pure function.

---

## 🔗 How It Connects to Other Phases

- **Phase 7 (Roles & Authorization)** builds on the service layer by adding role checks inside services — e.g., only Admins can delete runbooks. The service enforces this, not the UI.
- **Phase 8 (AI Assistant)** adds `IKnowledgeService.SearchAsync(string query)` — a semantic search method backed by pgvector similarity queries. The interface pattern established here makes adding new methods natural.
- **Phase 9 (RAG Pipeline)** extends `KnowledgeService` with embedding generation. The `CreateAsync` method will call the OpenAI embedding API after saving — all hidden from the UI behind the interface.
- **The Incidents page** will follow the same `@foreach` + create form pattern as Runbooks — a direct application of everything in Group 4.

---

## 📝 Quick Reference

```csharp
// Interface — contract only, no bodies
public interface IRunbookService
{
    Task<List<Runbook>> GetAllAsync();
    Task<Runbook?> GetByIdAsync(Guid id);
    Task CreateAsync(Runbook runbook);
    Task UpdateAsync(Runbook runbook);
    Task DeleteAsync(Guid id);
}

// Implementation — signs the contract
public class RunbookService : IRunbookService
{
    private readonly AppDbContext _db;
    public RunbookService(AppDbContext db) => _db = db;

    public async Task<List<Runbook>> GetAllAsync() =>
        await _db.Runbooks.OrderByDescending(r => r.CreatedAt).ToListAsync();
    // ... all other methods
}

// DI registration — wire interface to implementation
builder.Services.AddScoped<IRunbookService, RunbookService>();

// Component usage
@inject IRunbookService RunbookService

// EF Core save pattern
_db.Entities.Add(entity);       // stage insert
_db.Entities.Remove(entity);    // stage delete
_db.Entities.Update(entity);    // stage update
await _db.SaveChangesAsync();   // execute all staged SQL

// switch expression
private static string StatusColor(RunbookStatus status) => status switch
{
    RunbookStatus.Active     => "...",
    RunbookStatus.Deprecated => "...",
    _                        => "...",   // catch-all
};

// @foreach in Razor
@foreach (var item in list)
{
    <div>@item.Title</div>
}

// bool toggle
private bool showForm;
<button @onclick="() => showForm = !showForm">Toggle</button>
@if (showForm) { <div>...</div> }

// [SupplyParameterFromForm] null-safe pattern
[SupplyParameterFromForm]
private MyModel? model { get; set; }
protected override void OnInitialized() => model ??= new();
// Use model! in bindings since OnInitialized guarantees non-null
```