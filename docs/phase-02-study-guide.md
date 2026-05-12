# Phase 2 Study Guide — UI Shell

## 📖 Concepts Learned

### Tailwind CSS
- **Tailwind CSS** is a utility-first CSS framework. Instead of writing custom CSS classes like `.card { background: #1e1e2e; padding: 1rem; }`, you compose small single-purpose classes directly in your HTML — `bg-ctp-mantle p-4`. The styling lives where the markup lives.
- **The Tailwind CDN (Play CDN)** lets you use Tailwind without a build step. You drop a `<script>` tag in your HTML and Tailwind scans the DOM at runtime to inject only the styles you actually used.
- **`tailwind.config`** is a JavaScript object you pass to the CDN to extend or override the default theme. In Sentinel we used it to register Catppuccin Mocha colors under `ctp-*` names, and to register JetBrains Mono as the default monospace font.
- **`ctp-*`** stands for Catppuccin. The prefix namespaces the Catppuccin colors so they don't collide with Tailwind's built-in color names. `text-ctp-red` gets you Catppuccin's `#f38ba8` — not Tailwind's default red.

### CSS Flexbox
- **Flexbox** arranges elements along a single axis — either a row or a column. You set `display: flex` (or `flex` in Tailwind) on a container and its direct children become flex items.
- **`flex-1`** tells an item: *"take up all remaining space after fixed-size siblings have claimed theirs."* This is how the center content column fills the gap between the two fixed-width sidebars.
- **`flex-shrink-0`** tells an item: *"never get smaller, even when the container is tight."* Applied to both sidebars so they don't compress when the window narrows.
- **`flex-col`** changes the axis from horizontal to vertical — children stack top to bottom. Used inside sidebars to stack the header and the nav/content below it.
- **`h-screen` + `overflow-hidden`** on the outermost container locks the layout to exactly the browser window height. Scrolling then happens *inside* each panel independently, not on the page itself.

### CSS Grid
- **CSS Grid** arranges elements in two dimensions — rows AND columns simultaneously. Flexbox is one-directional; Grid is two-directional.
- **`grid grid-cols-3 gap-4`** creates a three-column grid where every column is equal width, with a gap between each cell. The browser calculates column widths automatically.
- **When to use Grid vs Flexbox:** use Flexbox when you're laying out items in a line (a nav bar, a row of buttons, a sidebar layout). Use Grid when you want items to snap into a structured two-dimensional arrangement (a card grid, a form, a data table).

### Blazor Routing
- **The `@page` directive** connects a `.razor` file to a URL. When someone navigates to `/runbooks`, Blazor scans all components for one that has `@page "/runbooks"` and renders it in place of `@Body`.
- **Creating a page = creating a file.** There is no route table to register and no config file to update. The file *is* the route.
- **`<Routes />`** in `App.razor` is the component that performs this scanning. It's the entry point for Blazor's router.

### Blazor Components
- **A reusable component** is any `.razor` file that does *not* have a `@page` directive. It is not a page — it is a piece of UI you can drop into other components or pages.
- **`@code { }`** is the C# block inside a `.razor` file. Logic, properties, and event handlers live here.
- **`[Parameter]`** is a C# attribute that marks a property as an input to the component. Whoever uses the component sets its value like an HTML attribute: `<StatCard Label="Incidents" />`.
- **Default values** (`= string.Empty`, `= "0"`) make components safe to use even if a parameter is forgotten. The component degrades gracefully instead of throwing an exception.
- **`@PropertyName`** renders a C# value into the HTML. The `@` symbol is Blazor's escape hatch from HTML into C#.

### Blazor `<NavLink>`
- **`<NavLink>`** is a Blazor built-in component that renders as an `<a>` tag but also watches the current URL. When its `href` matches the active route, it automatically adds a CSS class called `active` — and you can override that class name with `ActiveClass`.
- **`NavLinkMatch.All`** means: only apply the active class if the URL is an *exact* match. Use this for `/` (the home route) because every URL starts with `/` and would otherwise always be highlighted.
- **`NavLinkMatch.Prefix`** means: apply the active class if the URL *starts with* the href. Use this for `/runbooks`, `/incidents`, and any future nested routes like `/runbooks/new`.

---

## 🏗️ What We Built

### Groups completed
| Group | File(s) Changed | What |
|---|---|---|
| 1 | `App.razor` | Tailwind CDN, Catppuccin color config, JetBrains Mono font |
| 2 | `MainLayout.razor` | Three-column layout (nav / content / AI panel) |
| 3 | `MainLayout.razor` | Active nav links with `<NavLink>` |
| 4 | `Home.razor` | Dashboard with stat cards and recent activity stub |
| 5 | `Runbooks.razor`, `Incidents.razor` | Two new routed pages |
| 6 | `Shared/PageHeader.razor` | First reusable component — one `[Parameter]` |
| 7 | `Shared/StatCard.razor` | Second reusable component — three `[Parameter]`s |

### Component tree after Phase 2
```
App.razor
└── Routes.razor
    └── MainLayout.razor          (three-column shell)
        ├── [Left aside]          (nav with <NavLink>s)
        ├── @Body                 (active page renders here)
        │   ├── Home.razor
        │   │   ├── <PageHeader Title="Dashboard" />
        │   │   ├── <StatCard Label="Active Incidents" ... />
        │   │   ├── <StatCard Label="Runbooks" ... />
        │   │   └── <StatCard Label="Knowledge Base" ... />
        │   ├── Runbooks.razor
        │   │   └── <PageHeader Title="Runbooks" />
        │   └── Incidents.razor
        │       └── <PageHeader Title="Incidents" />
        └── [Right aside]         (AI assistant placeholder)
```

### Catppuccin color usage in Sentinel
| Color token | Hex | Used for |
|---|---|---|
| `ctp-base` | `#1e1e2e` | Center content background |
| `ctp-mantle` | `#181825` | Sidebar backgrounds, card backgrounds |
| `ctp-crust` | `#11111b` | Deepest background (unused in Phase 2) |
| `ctp-surface` | `#313244` | Borders, hover backgrounds |
| `ctp-muted` | `#6c7086` | Label text in cards |
| `ctp-subtle` | `#a6adc8` | Default nav link text |
| `ctp-text` | `#cdd6f4` | Primary text, active nav links, headings |
| `ctp-mauve` | `#cba6f7` | App name "Sentinel" |
| `ctp-teal` | `#94e2d5` | AI Assistant heading, Knowledge Base stat |
| `ctp-blue` | `#89b4fa` | Runbooks stat |
| `ctp-red` | `#f38ba8` | Active Incidents stat |

---

## 💡 Interview Q&A

**Q: What is the difference between Flexbox and CSS Grid?**
A: Flexbox is one-dimensional — it arranges items along a single axis (row or column). Grid is two-dimensional — it arranges items in rows and columns simultaneously. Use Flexbox for linear layouts like navbars and sidebars. Use Grid for structured arrangements like card grids and data tables.

**Q: What is a utility-first CSS framework?**
A: Instead of writing named CSS classes (`.btn`, `.card`) with properties inside them, you compose small single-purpose utility classes directly in your HTML (`bg-blue-500 px-4 py-2 rounded`). The styling and the markup stay together. Tailwind is the dominant utility-first CSS framework.

**Q: What is a Blazor component and how is it different from a page?**
A: Both are `.razor` files. A page has a `@page` directive that registers it with the router at a specific URL. A reusable component has no `@page` directive — it is just a piece of UI that you embed inside other components or pages. Pages are destinations; components are building blocks.

**Q: How do you pass data into a Blazor component?**
A: You declare a C# property inside a `@code { }` block and mark it with the `[Parameter]` attribute. The caller then sets it like an HTML attribute: `<MyComponent Title="Hello" />`. Inside the component, `@Title` renders the value.

**Q: Why does `<NavLink>` need `NavLinkMatch.All` for the home route?**
A: Because every URL starts with `/`. If you used `NavLinkMatch.Prefix` for `/`, the home link would be highlighted on every page in the app, not just the home page. `NavLinkMatch.All` restricts the match to the exact string `/`.

**Q: What is the Blazor router and how does it know which component to render?**
A: The router is the `<Routes />` component in `App.razor`. At startup it scans all compiled `.razor` files in the assembly for `@page` directives and builds a route table in memory. When the URL changes, it looks up the matching component and renders it in place of `@Body` in the active layout. No manual registration is needed — the `@page` directive is the registration.

---

## 🔗 How It Connects to Other Phases

- **Phase 3 (Domain)** defines the C# entity classes — `Runbook`, `Incident`, `KnowledgeEntry`. The `<StatCard Value="0" />` placeholders we built in Phase 2 will eventually display real counts fetched from these entities.
- **Phase 4 (Database)** wires EF Core and PostgreSQL. Once data exists in the database, Phase 2's dashboard cards will show live numbers.
- **Phase 5 (Authentication)** adds login/register pages. They will use the same `MainLayout.razor` shell and the `<PageHeader>` component built here.
- **Phase 8 (AI Assistant)** fills the right-side panel we stubbed out in Group 2. The panel is already positioned and styled — Phase 8 adds the chat input and the `IChatClient` wiring behind it.

---

## 📝 Quick Reference

```
Tailwind layout utilities used in Phase 2:
  flex              → enable flexbox on container
  flex-1            → grow to fill remaining space
  flex-shrink-0     → never compress below natural size
  flex-col          → stack children vertically
  h-screen          → height = 100vh (full browser window)
  overflow-hidden   → clip overflow on the outer container
  overflow-y-auto   → scroll vertically when content overflows
  grid              → enable CSS Grid on container
  grid-cols-3       → three equal-width columns
  gap-4             → 1rem gap between grid/flex children
  space-y-6         → 1.5rem vertical gap between siblings
  w-64              → fixed width 256px (left sidebar)
  w-80              → fixed width 320px (right panel)

Blazor component anatomy:
  @page "/route"                        → makes this a routed page
  @inherits LayoutComponentBase         → makes this a layout
  @Body                                 → renders the active page here
  @PropertyName                         → renders a C# value into HTML

  @code {
      [Parameter] public string X { get; set; } = string.Empty;
  }

NavLink match modes:
  NavLinkMatch.All      → exact URL match only (use for "/")
  NavLinkMatch.Prefix   → matches if URL starts with href (use for everything else)
```
