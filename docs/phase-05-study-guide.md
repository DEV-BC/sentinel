# Phase 5 Study Guide — Authentication with ASP.NET Core Identity

## 📖 Concepts Learned

### What is ASP.NET Core Identity?

🟢 **Simple:** Think of a building's security desk. It handles three things: proving who you are (authentication — login with email + password), checking what you're allowed to do (authorization — are you allowed in this room?), and keeping your credentials safe (passwords are never stored as plain text — they're hashed). ASP.NET Core Identity is that entire security desk, pre-built and ready to plug in.

🟡 **Real:** ASP.NET Core Identity is a complete membership system built into ASP.NET Core. It provides:
- `UserManager<TUser>` — creates, updates, deletes users, checks passwords
- `SignInManager<TUser>` — signs users in/out, manages the auth cookie
- `RoleManager<TRole>` — manages roles (Admin, Viewer, etc.)
- Password hashing (bcrypt by default — never stores plain text)
- Lockout (blocks users after too many failed attempts)
- Token providers (password reset, two-factor auth)

All of this comes from the `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package installed in Phase 1.

---

### `IdentityUser` and `ApplicationUser`

🟢 **Simple:** `IdentityUser` is Identity's standard visitor log format — it already has ~20 fields: email, password hash, phone number, lockout settings, and more. `ApplicationUser` is our custom version — it has everything the standard format has, plus our own fields. For now we added `DisplayName`.

🟡 **Real:** `ApplicationUser : IdentityUser` uses the same inheritance pattern as our domain entities. The base class handles the membership system; we only add what's specific to Sentinel.

```csharp
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
```

`ApplicationUser` lives in `Sentinel.Infrastructure` (not Domain) because it inherits from `IdentityUser`, which is an external dependency — and Domain must have zero external dependencies.

---

### `IdentityDbContext<TUser>`

🟢 **Simple:** `IdentityDbContext<ApplicationUser>` is a special subclass of `DbContext` that already includes all the Identity tables pre-wired. By changing `AppDbContext`'s base class from `DbContext` to `IdentityDbContext<ApplicationUser>`, our context gains seven new tables automatically — without writing a single property.

🟡 **Real:** `IdentityDbContext<TUser>` extends `DbContext` — so no existing functionality is lost. It adds `DbSet` properties for all Identity tables internally. EF Core detects the new tables and generates the migration automatically.

```csharp
// Before
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)

// After — gains all Identity tables
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
```

---

### The Seven Identity Tables

🟢 **Simple:** The security desk doesn't run on one filing system. It needs separate books for members, access levels, specific permissions, external logins, and temporary codes. Each is its own table.

🟡 **Real:**

| Table | Purpose |
|---|---|
| `AspNetUsers` | Every user account — includes our custom `DisplayName` column |
| `AspNetRoles` | Roles: Admin, Viewer, etc. |
| `AspNetUserRoles` | Junction table — which users have which roles |
| `AspNetUserClaims` | Fine-grained permissions per user |
| `AspNetRoleClaims` | Fine-grained permissions per role |
| `AspNetUserLogins` | External providers: Google, GitHub, etc. |
| `AspNetUserTokens` | Password reset tokens, two-factor codes |

The `AspNet` prefix is a legacy convention from older ASP.NET. These tables are managed entirely by Identity — you never write to them directly.

---

### Registering Identity in `Program.cs`

🟢 **Simple:** You've built the security desk. Now you officially staff it: hire the managers, give them the filing cabinet keys, set the ID rules (password length, required digits), and tell the building's entrance to check badges on every person who walks in.

🟡 **Real:** Three registrations are needed:

```csharp
// 1. Register Identity services and configure rules
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()   // use our database
.AddDefaultTokenProviders();                // enable reset/2FA tokens

// 2. Configure cookie paths to match our custom pages
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/login";
});

// 3. Make auth state available to all Blazor components
builder.Services.AddCascadingAuthenticationState();
```

And in the pipeline (after `builder.Build()`):
```csharp
app.UseAuthentication();   // who are you?
app.UseAuthorization();    // what are you allowed to do?
```

Order matters — authentication must run before authorization.

---

### Multiple Layouts in Blazor

🟢 **Simple:** Not every room in a building needs the same furniture. The main office has a three-column layout with a nav and an AI panel. The security desk (login/register) just needs a clean centered form — no sidebars, no nav.

🟡 **Real:** Blazor supports multiple layout components. Any `.razor` file that `@inherits LayoutComponentBase` is a layout. Pages opt into a specific layout with `@layout LayoutName`. Pages without a `@layout` directive use the default from `Routes.razor`.

```razor
@* AuthLayout.razor — minimal, centered *@
@inherits LayoutComponentBase

<div class="flex items-center justify-center h-screen bg-ctp-base">
    @Body
</div>
```

```razor
@* Login.razor — uses the minimal layout *@
@page "/login"
@layout AuthLayout
```

---

### Blazor Forms — `EditForm`, `@bind-Value`, Data Annotations

🟢 **Simple:** A form has fields (inputs), rules (validation), and a submit action. `EditForm` is Blazor's form wrapper. `@bind-Value` keeps a C# property in sync with what the user types. Data annotation attributes (`[Required]`, `[EmailAddress]`) declare the rules. `DataAnnotationsValidator` enforces them. `ValidationMessage` shows the errors.

🟡 **Real:**

```razor
<EditForm Model="model" OnValidSubmit="HandleSubmit" FormName="my-form">
    <DataAnnotationsValidator />

    <InputText @bind-Value="model.Email" />
    <ValidationMessage For="() => model.Email" />

    <button type="submit">Submit</button>
</EditForm>

@code {
    private readonly MyModel model = new();

    private async Task HandleSubmit()
    {
        // only called if all [Required], [EmailAddress] etc. pass
    }

    private sealed class MyModel
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    }
}
```

- `OnValidSubmit` — only fires if all validation passes. Use `OnSubmit` if you want to handle invalid submissions yourself.
- `FormName` — required in .NET 8+ for anti-forgery protection. Must be unique per form.
- `sealed class` inside `@code` — a model class that only this component needs. `sealed` means it can't be extended.

---

### Static SSR vs `@rendermode InteractiveServer` for Auth Pages

🟢 **Simple:** Blazor has two modes. **Interactive** mode keeps a live connection (WebSocket/SignalR) between browser and server — great for dynamic UIs. **Static SSR** mode sends plain HTML and handles forms as old-fashioned POST requests — no live connection, but the response can set browser cookies.

The security desk can only stamp your badge on the way *in* through the front door (a real HTTP response). It can't stamp it through a phone call (WebSocket). Login must use static SSR.

🟡 **Real:**

| | `@rendermode InteractiveServer` | Static SSR (no render mode) |
|---|---|---|
| `@bind-Value` works | ✅ Yes — live two-way sync | ❌ No — use `[SupplyParameterFromForm]` |
| Can set auth cookie | ❌ No — response already sent | ✅ Yes — real HTTP response |
| Form submission | Via SignalR | Via HTTP POST |
| Best for | Register, dynamic UI | Login, logout |

**Register** uses `@rendermode InteractiveServer` — it creates a user but doesn't set a cookie.

**Login** uses static SSR — it must set the auth cookie on the HTTP response.

---

### `[SupplyParameterFromForm]`

🟢 **Simple:** In interactive mode, `@bind-Value` keeps the model in sync as the user types. In static SSR, there's no live connection — the form submits as a plain POST and the model needs to be populated from that POST data. `[SupplyParameterFromForm]` does that job.

🟡 **Real:** Replace `private readonly ModelType model = new()` with:

```csharp
[SupplyParameterFromForm]
private LoginModel model { get; set; } = new();
```

Blazor reads the submitted form fields and maps them to the model properties automatically before `OnValidSubmit` fires. Note: the property must have a `set` accessor (not `readonly`) for the framework to populate it.

---

### `UserManager<T>` and `SignInManager<T>`

🟢 **Simple:** `UserManager` is the HR department — it creates employees (users), updates their records, and handles their credentials. `SignInManager` is the badge scanner — it checks your badge against the HR records, and either lets you in (sets the auth cookie) or rejects you.

🟡 **Real:** Both are injected from DI:

```razor
@inject UserManager<ApplicationUser> UserManager
@inject SignInManager<ApplicationUser> SignInManager
```

```csharp
// Creating a user
var user = new ApplicationUser { UserName = email, Email = email, DisplayName = name };
var result = await UserManager.CreateAsync(user, password);
// result.Succeeded → true/false
// result.Errors → list of IdentityError with Description

// Signing in
var result = await SignInManager.PasswordSignInAsync(email, password,
    isPersistent: false,   // session cookie vs persistent cookie
    lockoutOnFailure: false
);
// result.Succeeded → true/false
```

---

### Protecting Routes — `[Authorize]` and `<AuthorizeRouteView>`

🟢 **Simple:** `[Authorize]` is the lock on a door. `<AuthorizeRouteView>` is the master rule system: if someone tries to open a locked door without a badge, send them to the security desk. Without `<AuthorizeRouteView>`, the lock exists but nobody enforces it.

🟡 **Real:** Two changes work together:

**In `Routes.razor`** — replace `<RouteView>` with `<AuthorizeRouteView>`:
```razor
<AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)">
    <NotAuthorized>
        <RedirectToLogin />
    </NotAuthorized>
</AuthorizeRouteView>
```

**On each protected page:**
```razor
@attribute [Authorize]
```

`RedirectToLogin` is a small component that calls `Nav.NavigateTo("/login")` in `OnInitialized`. It exists as a component (rather than inline code) so the `<NotAuthorized>` slot stays clean.

---

### Logout — Minimal API Endpoint + Form POST

🟢 **Simple:** Signing out means destroying the badge. Like signing in, this requires the front door (a real HTTP response) — you can't destroy a badge through a phone call. A form POST goes through the real front door, the server clears the cookie, and you're sent back to the security desk.

🟡 **Real:** Two parts:

**In `Program.cs`** — a minimal API endpoint:
```csharp
app.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
}).RequireAuthorization();
```

**In the sidebar** — a form that POSTs to it:
```razor
<form method="post" action="/logout">
    <AntiForgeryToken />
    <button type="submit">Sign out</button>
</form>
```

`<AntiForgeryToken />` is a Blazor component that injects a hidden security token into the form. ASP.NET Core rejects POST requests without it — this prevents other websites from tricking your browser into submitting forms on your behalf (Cross-Site Request Forgery).

---

## 🔧 Troubleshooting

### "Fields are required" even after filling them in
**Cause:** The page was using static SSR (no `@rendermode`). In static SSR, `@bind-Value` doesn't sync the model in real-time — the model stays empty, validation fails immediately.

**Fix:** Add `@rendermode InteractiveServer` to pages that need live two-way binding (Register). For Login, use `[SupplyParameterFromForm]` instead of `@bind-Value` so the model is populated from the POST data.

---

### Login succeeds but doesn't redirect to the dashboard
**Cause:** With `@rendermode InteractiveServer`, `SignInManager.PasswordSignInAsync` tries to write the auth cookie to the HTTP response — but the response has already been sent (the connection is a WebSocket). The cookie is silently lost. Navigation happens but there's no cookie, so the user isn't authenticated.

**Fix:** Remove `@rendermode InteractiveServer` from `Login.razor`. Use static SSR (no render mode) so the form submits as a real HTTP POST — the server can set the cookie properly before responding. Switch from `@bind-Value` to `[SupplyParameterFromForm]` for model binding.

---

### Redirects to `/Account/Login` instead of `/login`
**Cause:** ASP.NET Core Identity's cookie middleware has `/Account/Login` as its built-in default login path. It doesn't know you built a custom page at `/login`.

**Fix:** Add `ConfigureApplicationCookie` to override the defaults:
```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/login";
});
```
This must be after `AddIdentity(...)` in the services section.

---

### "POST request does not specify which form is being submitted"
**Cause:** .NET 8+ requires every `EditForm` to have a unique name for anti-forgery identification.

**Fix:** Add `FormName` to every `EditForm`:
```razor
<EditForm Model="model" OnValidSubmit="HandleSubmit" FormName="unique-name">
```

---

### Auth cookie persists after testing — can't see the unauthenticated redirect
**Cause:** Your browser holds a valid auth cookie from a previous session, so you appear logged in even with protection enabled.

**Fix (quickest):** Open a private/incognito window.
**Fix (in DevTools):** Application → Cookies → localhost → delete `.AspNetCore.Identity.Application`.
**Fix (in app):** Click "Sign out" — the logout endpoint clears the cookie.

---

## 🏗️ What We Built

### Files created or modified
| File | Action | Purpose |
|---|---|---|
| `src/Sentinel.Infrastructure/Identity/ApplicationUser.cs` | Created | Custom user entity extending `IdentityUser` |
| `src/Sentinel.Infrastructure/AppDbContext.cs` | Modified | Changed base class to `IdentityDbContext<ApplicationUser>` |
| `src/Sentinel.Infrastructure/Migrations/` | Generated | `AddIdentity` migration — creates seven Identity tables |
| `src/Sentinel.Web/Program.cs` | Modified | Added Identity services, cookie config, auth middleware, logout endpoint |
| `src/Sentinel.Web/Components/Layout/AuthLayout.razor` | Created | Minimal centered layout for auth pages |
| `src/Sentinel.Web/Components/Layout/MainLayout.razor` | Modified | Added logout form to sidebar |
| `src/Sentinel.Web/Components/Pages/Login.razor` | Created | Static SSR login form with `[SupplyParameterFromForm]` |
| `src/Sentinel.Web/Components/Pages/Register.razor` | Created | Interactive register form with `@rendermode InteractiveServer` |
| `src/Sentinel.Web/Components/Routes.razor` | Modified | Replaced `<RouteView>` with `<AuthorizeRouteView>` |
| `src/Sentinel.Web/Components/Shared/RedirectToLogin.razor` | Created | Redirect component used in `<NotAuthorized>` slot |
| `src/Sentinel.Web/Components/Pages/Home.razor` | Modified | Added `@attribute [Authorize]` |
| `src/Sentinel.Web/Components/Pages/Runbooks.razor` | Modified | Added `@attribute [Authorize]` |
| `src/Sentinel.Web/Components/Pages/Incidents.razor` | Modified | Added `@attribute [Authorize]` |
| `src/Sentinel.Web/Components/_Imports.razor` | Modified | Added Identity, Infrastructure, and DataAnnotations usings |

---

## 💡 Interview Q&A

**Q: What is ASP.NET Core Identity and what does it provide out of the box?**
A: Identity is a complete membership system for ASP.NET Core. It provides user management (`UserManager`), sign-in management (`SignInManager`), role management (`RoleManager`), secure password hashing, account lockout, and token providers for password reset and two-factor authentication. It integrates directly with EF Core via `IdentityDbContext<TUser>` and creates all required tables automatically through migrations.

**Q: Why does `ApplicationUser` live in Infrastructure and not Domain?**
A: Domain must have zero external dependencies — pure C# only. `ApplicationUser` inherits from `IdentityUser`, which comes from `Microsoft.AspNetCore.Identity`. That's an external dependency, so `ApplicationUser` belongs in Infrastructure where external dependencies are allowed.

**Q: What is the difference between authentication and authorization?**
A: Authentication answers "who are you?" — verifying identity, typically via email + password. Authorization answers "what are you allowed to do?" — checking permissions after identity is confirmed. In ASP.NET Core, `UseAuthentication()` runs first (reads the cookie and establishes identity), then `UseAuthorization()` runs (checks if that identity has access). Order in the pipeline is mandatory.

**Q: Why can't you set an auth cookie from a Blazor Server interactive component?**
A: In Blazor Server's interactive mode, the browser and server communicate over a persistent WebSocket (SignalR). The initial HTTP response has already been sent and completed — the connection is now a WebSocket. Setting a cookie requires writing to an HTTP response header, which is impossible on a WebSocket. Login and logout must happen through real HTTP requests (static SSR forms or API endpoints) so the server can write cookie headers to the response.

**Q: What is `[SupplyParameterFromForm]` and when do you use it?**
A: It's an attribute that tells Blazor to populate a property from submitted form data during static SSR. In interactive mode, `@bind-Value` keeps the model in sync in real time. In static SSR there's no live connection — the form submits as a plain HTTP POST. `[SupplyParameterFromForm]` maps the POST fields to the model properties before `OnValidSubmit` fires.

**Q: What is an anti-forgery token and why is it required on forms?**
A: An anti-forgery token (also called CSRF token) is a unique secret value embedded in every form. When the form is submitted, ASP.NET Core verifies the token matches what it issued. This prevents Cross-Site Request Forgery attacks — where a malicious website tricks your browser into submitting a form to a different site while using your cookies. ASP.NET Core rejects any POST request without a valid token.

**Q: What does `ConfigureApplicationCookie` do and why was it needed?**
A: Identity's cookie middleware has built-in defaults for where to redirect unauthenticated users — `/Account/Login` by default. `ConfigureApplicationCookie` overrides those defaults to point at our custom pages (`/login`, `/logout`). Without it, Identity's redirect ignores your custom pages entirely.

---

## 🔗 How It Connects to Other Phases

- **Phase 6 (Application Layer)** introduces services (`IRunbookService`, `IIncidentService`). The `ApplicationUser` entity can be referenced in services to scope data per user — e.g., "runbooks created by this user."
- **Phase 7 (Roles & Authorization)** adds role-based access control on top of Phase 5's foundation. Admins get different pages and actions than regular users. `RoleManager` and `[Authorize(Roles = "Admin")]` build on what's here.
- **Phase 8 (AI Assistant)** will use the logged-in user's identity to scope AI conversations — each user's chat history is separate.

---

## 📝 Quick Reference

```
Authentication flow:
  Register → UserManager.CreateAsync(user, password) → redirect to /login
  Login    → SignInManager.PasswordSignInAsync(email, password) → set cookie → redirect to /
  Logout   → SignInManager.SignOutAsync() → clear cookie → redirect to /login

Static SSR vs Interactive:
  Register page → @rendermode InteractiveServer  (no cookie needed)
  Login page    → no render mode / static SSR    (must set cookie)
  Logout        → minimal API POST endpoint      (must clear cookie)

Protecting pages:
  @attribute [Authorize]           → lock the door
  <AuthorizeRouteView>             → enforce the lock, redirect if not authenticated
  <NotAuthorized><RedirectToLogin /></NotAuthorized>  → where to send them

Key services (all injected from DI):
  UserManager<ApplicationUser>   → create/update/delete users
  SignInManager<ApplicationUser> → sign in / sign out
  NavigationManager              → programmatic navigation

Data annotation validators:
  [Required]          → field must have a value
  [EmailAddress]      → must be a valid email format
  [MinLength(n)]      → string must be at least n characters
  [MaxLength(n)]      → string must be no more than n characters
```