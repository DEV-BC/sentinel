# Phase 1 Study Guide — Solution & Project Setup

## 📖 Concepts Learned

### .NET & the CLI
- **.NET** is a platform that lets you write C# and run it anywhere — Windows, Mac, Linux, servers, browsers (via WebAssembly). Version numbers follow a yearly release cadence (8, 9, 10...).
- **The `dotnet` CLI** is how you create projects, run them, add packages, and build without an IDE. Every command you ran in this phase went through it.
- **A solution (`.sln`)** is a registry file that lists which projects belong together. It doesn't contain logic — it's a manifest, like a table of contents.
- **A project (`.csproj`)** is a single compilable unit. It knows its target framework, its dependencies, and what NuGet packages it uses.

### Blazor
- **Blazor** is a framework for building web UIs in C# instead of JavaScript.
- **Blazor Server** runs all C# on the server. The browser is a display. A persistent WebSocket connection called a **SignalR circuit** carries events from the browser to the server and pushes DOM updates back. Only the changed parts of the page travel over the wire — not the whole page.
- **Blazor WebAssembly** downloads the entire .NET runtime into the browser and runs C# locally. Better for offline/public apps. Worse for internal tools needing real-time updates.
- **Why we chose Blazor Server for Sentinel:** real-time metrics, streaming AI, internal tool (always connected), simpler learning model (one world: the server).

### Clean Architecture
- **Dependency rules** are enforced by the compiler via `<ProjectReference>` entries in `.csproj` files. If you violate a rule, the build fails — not just a convention you pinky-promise to follow.
- **Domain** knows nothing. **Application** knows Domain. **Infrastructure** knows Application + Domain. **Web** knows everything (it's the composition root).
- The composition root is the one place where all layers are allowed to meet. In Sentinel it is `Program.cs` inside `Sentinel.Web`.

### Blazor File Structure
- **`App.razor`** — the only file with a real HTML document (`<!DOCTYPE html>`). The outer shell for everything.
- **`Routes.razor`** — the router. Scans the assembly for `@page` directives and maps URLs to components.
- **`MainLayout.razor`** — the layout wrapper. Every page gets wrapped in this automatically unless it opts out.
- **`_Imports.razor`** — global `@using` statements. Anything here is available in every `.razor` file in the same folder tree.
- **`Program.cs`** — two phases: (1) register services into the DI container, (2) configure the HTTP request pipeline. Every future phase adds to this file.

### NuGet
- **NuGet** is the .NET package registry. `dotnet add package` installs a package into a specific project.
- Packages only affect the project they're installed into. Installing a package in Infrastructure does not make it available in Domain.

---

## 🏗️ What We Built

### Files created
| File | Decision |
|---|---|
| `Sentinel.sln` | The solution registry. Lists all four projects. |
| `src/Sentinel.Domain/` | Class library. No references, no packages. Pure C# forever. |
| `src/Sentinel.Application/` | Class library. References Domain. Houses interfaces and business logic. |
| `src/Sentinel.Infrastructure/` | Class library. References Application + Domain. All external concerns. |
| `src/Sentinel.Web/` | Blazor Web App. References all three. The only runnable project. |
| `docs/phase-01-study-guide.md` | This file. |

### Dependency graph (enforced by compiler)
```
Domain ←── Application ←── Infrastructure
                  ↑                ↑
                  └────── Web ─────┘
                           ↑
                    (composition root)
```

### Packages installed
| Package | Project | Purpose |
|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Infrastructure | EF Core ORM + PostgreSQL driver |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Infrastructure | Authentication system tied to EF Core |
| `Pgvector` | Infrastructure | `Vector` type for storing AI embeddings |
| `Microsoft.Extensions.AI.OpenAI` | Infrastructure | OpenAI implementation of AI abstractions |
| `PdfPig` | Infrastructure | Extract text from PDF files |
| `DocumentFormat.OpenXml` | Infrastructure | Extract text from Word (.docx) files |
| `Microsoft.Extensions.AI.Abstractions` | Application | `IChatClient`, `IEmbeddingGenerator` interfaces — no implementation |
| `Microsoft.EntityFrameworkCore.Design` | Web | Enables `dotnet ef migrations` tooling |

### Key decisions made
1. **Blazor Server over WebAssembly** — real-time requirements + internal tool + learning simplicity.
2. **`--empty` template** — no sample pages. We build our own UI from scratch.
3. **`--interactivity Server`** — enables SignalR circuit; without this, no C# interactivity in the browser.
4. **Abstractions in Application, OpenAI in Infrastructure** — Application defines what AI should do, Infrastructure decides which provider does it. Swap providers by changing Infrastructure only.

---

## 💡 Interview Q&A

**Q: What is the difference between Blazor Server and Blazor WebAssembly?**
A: Blazor Server runs all C# on the server and uses a SignalR WebSocket connection to push UI updates to the browser. The browser downloads nothing except a small JavaScript bridge. Blazor WebAssembly downloads the entire .NET runtime to the browser and runs C# locally. Server is better for real-time internal tools; WebAssembly is better for offline or public-facing apps.

**Q: What is Clean Architecture and why does it matter?**
A: Clean Architecture separates code into concentric layers with strict dependency rules — inner layers know nothing about outer layers. This means you can swap your database, your UI framework, or your AI provider without touching your business logic. In .NET, these rules are enforced at compile time via project references, not just convention.

**Q: What is a composition root?**
A: It's the single place in an application where all the dependency injection wiring happens. Every service gets registered here and only here. In an ASP.NET Core app, that's `Program.cs`. It's intentionally the one place in the codebase where all layers are allowed to reference each other.

**Q: Why put AI abstractions in the Application layer instead of Infrastructure?**
A: The Application layer defines *what* the system needs to do (interfaces). Infrastructure defines *how* it's done (implementations). `IChatClient` and `IEmbeddingGenerator` are contracts — they belong in Application so the business logic can depend on the interface without knowing whether OpenAI, Azure OpenAI, or Ollama is behind it.

**Q: What does `dotnet add package` actually do?**
A: It writes a `<PackageReference>` entry into the target `.csproj` file and downloads the package to the local NuGet cache. The package is only available inside that project — installing it in one project does not make it available in another.

---

## 🔗 How It Connects to Other Phases

- **Phase 2 (UI Shell)** builds inside `Sentinel.Web` — specifically inside `MainLayout.razor` and new components. The three-column Catppuccin layout replaces the empty shell we have now.
- **Phase 3 (Domain)** fills `Sentinel.Domain` with entity classes. The project exists and compiles, but is currently empty except for a placeholder class.
- **Phase 4 (Database)** fills `Sentinel.Infrastructure` with EF Core's `DbContext` and repository implementations. The NuGet packages we installed today (`Npgsql`, `Identity`) get used for the first time.
- **Phase 5 (Authentication)** uses `Microsoft.AspNetCore.Identity.EntityFrameworkCore` installed today. Identity is already available — Phase 5 wires it into `Program.cs` and builds the login/register UI.
- **Phase 8 (AI Assistant)** uses `Microsoft.Extensions.AI.OpenAI` installed today. The `IChatClient` interface we installed via Abstractions gets its first implementation wired in `Program.cs`.
- **Phase 9 (RAG Pipeline)** uses `Pgvector`, `PdfPig`, and `DocumentFormat.OpenXml` installed today.

---

## 📝 Quick Reference

```bash
# Create a solution
dotnet new sln -n ProjectName

# Create a class library project
dotnet new classlib -n ProjectName -o path/to/folder

# Create a Blazor Server project
dotnet new blazor -n ProjectName -o path/to/folder --interactivity Server --empty

# Register a project with the solution
dotnet sln add path/to/Project.csproj

# List all projects in a solution
dotnet sln list

# Add a project reference (A references B)
dotnet add A.csproj reference B.csproj

# Add a NuGet package to a project
dotnet add path/to/Project.csproj package PackageName

# Build the entire solution
dotnet build

# Run a specific project
dotnet run --project path/to/Project
```

```
Blazor component tree (top to bottom):
App.razor → Routes.razor → MainLayout.razor → [YourPage].razor

Request flow:
Browser → SignalR → Program.cs pipeline → App.razor → Routes.razor → Page component → DOM diff back to browser
```