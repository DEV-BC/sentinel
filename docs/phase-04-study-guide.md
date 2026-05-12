# Phase 4 Study Guide — Infrastructure Layer: Database

## 📖 Concepts Learned

### What is an ORM?

🟢 **Simple:** Think of a translator at the United Nations. The delegates speak their own language — C# objects in our case. The database speaks its own language — SQL tables and rows. An ORM (Object-Relational Mapper) is the translator sitting between them. You hand it a `Runbook` object and it writes the SQL. You ask it for all open incidents and it runs the query and hands you back C# objects. You never write SQL directly.

🟡 **Real:** EF Core is .NET's ORM. It reads your C# classes and generates SQL for you. It tracks changes to your objects and knows which ones need to be saved. It handles migrations — schema changes over time. The configuration lives entirely in `Sentinel.Infrastructure`, the one layer that is allowed to know about the database.

---

### `DbContext` and `DbSet<T>`

🟢 **Simple:** Think of a filing cabinet. The cabinet itself is the `AppDbContext` — the central thing that manages everything. Each **drawer** in the cabinet is a `DbSet<T>` — one drawer for Runbooks, one for Incidents, one for KnowledgeEntries. You don't touch the physical files directly. You ask the cabinet to file something or retrieve something, and it handles all the organization.

🟡 **Real:** `DbContext` is the central EF Core class. You extend it using inheritance (same pattern as `BaseEntity`). `DbSet<T>` is one property per entity — each one represents a database table. `DbSet<Runbook>` maps to a `Runbooks` table. Queries and saves go through these properties.

```csharp
// Extending DbContext — same inheritance pattern as BaseEntity
public class AppDbContext : DbContext
{
    // Each DbSet<T> = one table
    public DbSet<Runbook> Runbooks => Set<Runbook>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<KnowledgeEntry> KnowledgeEntries => Set<KnowledgeEntry>();
}
```

---

### Primary Constructors (C# 12)

🟢 **Simple:** Instead of writing a constructor body with `options` being passed in, C# 12 lets you put the constructor arguments right after the class name. It's a shorthand — the two forms do exactly the same thing.

🟡 **Real:** The traditional constructor syntax and the primary constructor syntax are equivalent:

```csharp
// Traditional constructor (what was suggested)
public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

// Primary constructor (C# 12 — what you wrote, equally correct)
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
```

Both receive `DbContextOptions` from the DI container and pass it up to the parent `DbContext`. You'll never call this yourself — the framework does it automatically.

---

### `OnModelCreating` and the Fluent API

🟢 **Simple:** EF Core is smart enough to figure out most things automatically — it sees a `string` property and creates a `TEXT` column, it sees a `Guid` and creates a `UUID` column. But some things it can't guess. `OnModelCreating` is where you give it extra instructions it couldn't figure out on its own.

🟡 **Real:** `OnModelCreating` is a method you override to configure things EF Core can't infer automatically. In Sentinel, we use it for two things:
- `HasPostgresExtension("vector")` — tell PostgreSQL to enable the pgvector extension
- Value converter + column type for `float[]` — tell EF Core how to store embeddings

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.HasPostgresExtension("vector");

    modelBuilder.Entity<KnowledgeEntry>()
        .Property(e => e.Embedding)
        .HasConversion(
            v => new Vector(v!),
            v => v.ToArray(),
            new ValueComparer<float[]>(
                (a, b) => a!.SequenceEqual(b!),
                a => a.Aggregate(0, (h, f) => HashCode.Combine(h, f.GetHashCode())),
                a => a.ToArray()
            )
        )
        .HasColumnType("vector(1536)");
}
```

---

### Value Converters and Value Comparers

🟢 **Simple:** The Domain layer uses `float[]` for embeddings (pure C# — no database knowledge). The database stores vectors in a special `vector(1536)` column format. A value converter is a translator between the two — on the way in, convert `float[]` to `Vector`; on the way out, convert back to `float[]`.

A value comparer tells EF Core how to detect when an embedding has changed. Without it, EF Core can't tell if you updated the array values, and your changes might silently not be saved.

🟡 **Real:** `HasConversion` takes three lambdas — going to DB, coming from DB, and an optional `ValueComparer<T>`. The `ValueComparer` takes three lambdas:
1. **Equality** — how to compare two values (`SequenceEqual` for arrays)
2. **Hash code** — how to hash a value (for change tracking dictionaries)
3. **Snapshot** — how to make a deep copy so EF Core remembers the original

---

### `appsettings.json` and Connection Strings

🟢 **Simple:** Think of a phone book. Your app knows *how* to call the database, but it needs to know *which number to dial* — the address, the port, the username, the password. You don't hardcode a phone number into source code. You put it in a config file so you can change it without recompiling.

🟡 **Real:** `appsettings.json` is ASP.NET Core's built-in configuration file, loaded automatically at startup. The `ConnectionStrings` section is a convention EF Core understands. `builder.Configuration.GetConnectionString("DefaultConnection")` reads the value at runtime.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=sentinel;Username=sentinel;Password=sentinel"
  }
}
```

The connection string format is Npgsql's — each key-value pair maps to a PostgreSQL connection parameter.

---

### Registering `AppDbContext` with Dependency Injection

🟢 **Simple:** You've built the filing cabinet. Now you need to tell the building's reception desk ("the DI container") that the cabinet exists and how to find it. From that point on, anyone who asks the front desk for "the filing cabinet" gets handed one automatically — they don't build it themselves.

🟡 **Real:** `builder.Services.AddDbContext<AppDbContext>` registers EF Core with ASP.NET Core's dependency injection container. From this point on, anything that declares `AppDbContext` as a dependency (a service, a Blazor component) will have one built and injected automatically. You never write `new AppDbContext(...)`.

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseVector()
    ));
```

`UseVector()` activates Npgsql's pgvector plugin, which teaches it how to handle the `Vector` type from the `Pgvector` package.

---

### EF Core Migrations

🟢 **Simple:** When an architect updates blueprints for a building, they don't tear the building down and rebuild it. They issue a **change order** — a document that says exactly what to add, remove, or modify. The construction crew applies only the changes. EF Core migrations work the same way. Each migration is a change order describing what the database schema needs to become.

🟡 **Real:** Two commands do the work:
- `dotnet ef migrations add [Name]` — generates a C# file describing the schema changes. **Nothing touches the database yet.** EF Core compares your current `AppDbContext` to the last known state and writes the diff.
- `dotnet ef database update` — connects to the database and runs all pending migrations. This is when SQL actually executes and tables get created or modified.

Both commands need `--project` (where `AppDbContext` lives) and `--startup-project` (where the connection string and DI registration live):

```bash
dotnet ef migrations add InitialCreate \
  --project src/Sentinel.Infrastructure \
  --startup-project src/Sentinel.Web

dotnet ef database update \
  --project src/Sentinel.Infrastructure \
  --startup-project src/Sentinel.Web
```

---

### Docker and Volumes

🟢 **Simple:** A Docker container is like a hotel room — everything you put in it exists while you're there, but when you check out (remove the container) the room gets wiped. A **volume** is like a storage unit attached to the hotel. Your stuff lives in the storage unit, not the room. Check out and back in — your data is still there.

🟡 **Real:** Without a volume, `docker rm sentinel-db` deletes your database data permanently. With a named volume (`-v sentinel-db-data:/var/lib/postgresql/data`), Docker stores the PostgreSQL data files on your machine independently of the container. The container is disposable; the volume persists.

```bash
# Start with a persistent volume
docker run -d \
  --name sentinel-db \
  -e POSTGRES_USER=sentinel \
  -e POSTGRES_PASSWORD=sentinel \
  -e POSTGRES_DB=sentinel \
  -p 5432:5432 \
  -v sentinel-db-data:/var/lib/postgresql/data \
  pgvector/pgvector:pg17

docker stop sentinel-db    # pauses — data preserved
docker start sentinel-db   # resumes — data still there
docker volume rm sentinel-db-data  # THIS deletes the data
```

---

### `@inject` and `OnInitializedAsync` in Blazor

🟢 **Simple:** `@inject` is how a Blazor component asks the front desk ("the DI container") for something it needs. `OnInitializedAsync` is the moment right before the component appears on screen — the perfect time to go fetch data from the database.

🟡 **Real:** `@inject AppDbContext Db` tells the DI container to provide an `AppDbContext` instance and assign it to the field `Db`. `OnInitializedAsync` is a Blazor lifecycle method called once when the component initializes. `CountAsync()` is an EF Core extension method that runs `SELECT COUNT(*) FROM ...` asynchronously.

```razor
@inject AppDbContext Db

@code {
    private int runbookCount;

    protected override async Task OnInitializedAsync()
    {
        runbookCount = await Db.Runbooks.CountAsync();
    }
}
```

---

## 🔧 Troubleshooting

### "UseVector is not an option"
**Cause:** `UseVector()` is provided by the `Pgvector.EntityFrameworkCore` package, which was not installed. The base `Pgvector` package provides the `Vector` type but not the EF Core integration.

**Fix:** Install the missing package in `Sentinel.Web` (where `Program.cs` lives):
```bash
dotnet add src/Sentinel.Web/Sentinel.Web.csproj package Pgvector.EntityFrameworkCore
```
Then add `using Pgvector.EntityFrameworkCore;` in `Program.cs` and use `o => o.UseVector()`.

---

### "float[] could not be mapped to vector(1536)"
**Cause:** Npgsql doesn't know how to map `float[]` to a `vector` column without the pgvector plugin enabled AND a value converter teaching it the translation.

**Fix:** Two things together:
1. `UseVector()` in the `UseNpgsql` options (enables the plugin)
2. `HasConversion(v => new Vector(v!), v => v.ToArray())` in `OnModelCreating` (teaches the translation)

---

### C# 14 warning: "Resolution for SequenceEqual has changed due to spans"
**Cause:** C# 14 added new `SequenceEqual` overloads for `Span<T>`. When called on `float[]`, the compiler sees both the LINQ overload (`IEnumerable<T>`) and the new span overload and warns about ambiguity.

**Attempted fix 1:** `a!.AsSpan().SequenceEqual(b!)` — failed because `Span<T>` is a `ref struct` and cannot be used inside expression trees (which `ValueComparer` uses).

**Attempted fix 2:** `a!.SequenceEqual(b!, EqualityComparer<float>.Default)` — still triggered the warning in C# 14.

**Current status:** Warning accepted for now. It does not break compilation or the migration. Will be resolved properly in Phase 8 when embedding updates are implemented.

---

### "fail: Failed executing DbCommand — SELECT from __EFMigrationsHistory" on first `database update`
**Cause:** This is **expected behavior**, not a real error. On a brand new database, EF Core first checks the `__EFMigrationsHistory` table to see which migrations have already run. That table doesn't exist yet on a fresh database, so the query fails. EF Core catches this, creates the table itself, then applies all pending migrations.

**How to confirm it worked:** Look for `Done.` at the end of the output. Also run:
```bash
docker exec -it sentinel-db psql -U sentinel -d sentinel -c "\dt"
```
You should see `Incidents`, `KnowledgeEntries`, `Runbooks`, and `__EFMigrationsHistory`.

---

## 🏗️ What We Built

### Files created or modified
| File | Action | Purpose |
|---|---|---|
| `src/Sentinel.Infrastructure/AppDbContext.cs` | Created | EF Core DbContext with DbSets and vector configuration |
| `src/Sentinel.Infrastructure/Migrations/` | Generated | Auto-generated migration files — do not edit manually |
| `src/Sentinel.Web/appsettings.json` | Modified | Added `ConnectionStrings:DefaultConnection` |
| `src/Sentinel.Web/Program.cs` | Modified | Registered `AppDbContext` with `AddDbContext` + `UseVector` |
| `src/Sentinel.Web/Components/_Imports.razor` | Modified | Added `@using Sentinel.Infrastructure` globally |
| `src/Sentinel.Web/Components/Pages/Home.razor` | Modified | Live database counts via `@inject` and `CountAsync` |

### Packages added
| Package | Project | Purpose |
|---|---|---|
| `Pgvector.EntityFrameworkCore` | Sentinel.Web | Provides `UseVector()` extension for Npgsql |

### Database tables created
| Table | Maps to |
|---|---|
| `Runbooks` | `Runbook` entity |
| `Incidents` | `Incident` entity |
| `KnowledgeEntries` | `KnowledgeEntry` entity |
| `__EFMigrationsHistory` | EF Core internal — tracks which migrations have run |

---

## 💡 Interview Q&A

**Q: What is EF Core and what problem does it solve?**
A: EF Core is .NET's ORM (Object-Relational Mapper). It translates between C# objects and database tables, generating SQL from your C# queries and mapping result rows back to objects. It eliminates hand-written SQL for standard operations and handles schema changes through migrations, so the database schema stays in sync with your C# models automatically.

**Q: What is a DbContext and what is a DbSet?**
A: `DbContext` is EF Core's central class — it manages database connections, tracks changes to entities, and coordinates saves. `DbSet<T>` is one property per entity type on the context, representing a table. You query and save entities through their `DbSet`.

**Q: What is a migration and why do you need one?**
A: A migration is a C# file that describes a set of schema changes — create this table, add that column, rename this index. EF Core generates migrations automatically by comparing your current model to the last known schema. Running `database update` applies pending migrations to the actual database. This gives you a versioned history of schema changes that can be replayed on any environment.

**Q: What is a value converter in EF Core?**
A: A value converter teaches EF Core how to translate a C# type to a database type that it can't handle automatically. In Sentinel, `float[]` (pure C#) cannot be stored directly as a `vector(1536)` column. The converter says: going to the DB, wrap it in `Vector`; coming from the DB, call `.ToArray()`. This keeps the Domain layer clean (it uses `float[]`, not the Pgvector-specific `Vector` type).

**Q: Why does `AppDbContext` live in Infrastructure and not in Web or Domain?**
A: Clean Architecture places database concerns in Infrastructure — the layer specifically designed to hold external dependencies. Domain must stay dependency-free (pure C#). Web is the composition root that wires everything together but shouldn't own database logic. Infrastructure is allowed to reference both Domain and Application, making it the right home for EF Core which needs to know about your entities (Domain) and implement repository interfaces (Application).

**Q: What is `@inject` in Blazor?**
A: `@inject` is Blazor's syntax for requesting a service from the dependency injection container. When you write `@inject AppDbContext Db`, Blazor asks the DI container for an `AppDbContext` instance and assigns it to `Db`. You never construct it yourself — the framework handles lifetime, connection pooling, and disposal.

---

## 🔗 How It Connects to Other Phases

- **Phase 5 (Authentication)** adds ASP.NET Core Identity. `AppDbContext` will be updated to extend `IdentityDbContext<ApplicationUser>` instead of plain `DbContext`, and a new migration will add Identity tables (`AspNetUsers`, `AspNetRoles`, etc.) to the same database.
- **Phase 6 (Application Layer)** introduces service interfaces (`IRunbookService`, `IIncidentService`). The direct `@inject AppDbContext Db` in `Home.razor` will be replaced with injected services — a cleaner separation between the UI and data access.
- **Phase 8 (AI Assistant)** uses `Db.KnowledgeEntries` with vector similarity queries to implement semantic search. The `float[]` embedding column and the value converter configured here are what make that possible.

---

## 📝 Quick Reference

```bash
# Start PostgreSQL with persistent volume
docker run -d --name sentinel-db \
  -e POSTGRES_USER=sentinel -e POSTGRES_PASSWORD=sentinel -e POSTGRES_DB=sentinel \
  -p 5432:5432 -v sentinel-db-data:/var/lib/postgresql/data \
  pgvector/pgvector:pg17

docker stop sentinel-db        # pause (data safe)
docker start sentinel-db       # resume
docker ps                      # list running containers
docker exec -it sentinel-db psql -U sentinel -d sentinel -c "\dt"  # list tables

# EF Core CLI
dotnet tool install --global dotnet-ef   # one-time global install

dotnet ef migrations add [Name] \
  --project src/Sentinel.Infrastructure \
  --startup-project src/Sentinel.Web    # generate a migration

dotnet ef database update \
  --project src/Sentinel.Infrastructure \
  --startup-project src/Sentinel.Web    # apply migrations to database

dotnet ef migrations remove \
  --project src/Sentinel.Infrastructure \
  --startup-project src/Sentinel.Web    # undo last migration (before database update)
```

```csharp
// Blazor component reading from the database
@inject AppDbContext Db

@code {
    private int count;

    protected override async Task OnInitializedAsync()
    {
        count = await Db.Runbooks.CountAsync();
    }
}

// Lifecycle order in a Blazor component:
// 1. Component is constructed (DI injections happen)
// 2. OnInitialized / OnInitializedAsync — fetch data here
// 3. Component renders
// 4. OnAfterRender / OnAfterRenderAsync — DOM is ready
```