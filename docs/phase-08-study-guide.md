# Phase 8 Study Guide: AI Integration (RAG)

You built the full AI pipeline: knowledge entries are stored with embeddings, searched by meaning, and used to ground the AI chat panel's answers. This is called RAG — Retrieval Augmented Generation — and it's the foundation of most real-world AI assistants.

---

## The Big Picture: What Is RAG?

**🟢 Simple:**
A regular AI chatbot answers from memory (its training data). RAG is like giving the AI an open-book exam — you first find the relevant pages from your own documents, hand them to the AI, and say "answer using only these." The AI's answer is grounded in *your* data, not guesswork.

**🟡 Real:**
RAG has three steps:
1. **Retrieve** — embed the user's question, search the knowledge base for the closest entries by vector distance
2. **Augment** — inject those entries into the system prompt as context
3. **Generate** — send system prompt + question to the language model, get a grounded answer

```
User question
    → IEmbeddingGenerator → float[1536]
    → IKnowledgeService.SearchAsync() → top 3 entries
    → system prompt built with entry content
    → IChatClient.GetResponseAsync() → answer
    → displayed in sidebar with sources listed
```

---

## Group 1: What Are Embeddings? + API Setup

### 🟢 Simple — Embeddings
Every piece of text has a secret GPS coordinate — but instead of 2 numbers (lat/long), it has 1,536 numbers. Texts that *mean similar things* land at nearby coordinates, even if they use different words. "Server is down" and "database crashed" are neighbors. "My cat is fluffy" is far away. An embedding is that list of 1,536 numbers encoding the *meaning* of text.

### 🟢 Simple — Semantic Search
Instead of Ctrl+F (exact word match), semantic search finds entries that *mean* what you asked. You search "why is the site slow?" and it finds "Database Query Optimization" because the meanings are nearby on the embedding map.

### 🟡 Real — Two Key Interfaces
`Microsoft.Extensions.AI` provides two interfaces you registered in `Program.cs`:

| Interface | What It Does |
|-----------|-------------|
| `IEmbeddingGenerator<string, Embedding<float>>` | Sends text to OpenAI, gets back 1,536 floats |
| `IChatClient` | Sends a conversation to OpenAI, gets back a response |

### User Secrets (keeping API keys safe)
Never put API keys in `appsettings.json` — that file gets committed to git. User secrets store them outside the project folder, local to your machine only.

```bash
# One-time setup per project
dotnet user-secrets init --project src/Sentinel.Web

# Store your key
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project src/Sentinel.Web
```

User secrets are automatically loaded in development — `builder.Configuration["OpenAI:ApiKey"]` just works.

### Registering the Services in Program.cs
```csharp
using Microsoft.Extensions.AI;
using OpenAI;

var openAIClient = new OpenAIClient(builder.Configuration["OpenAI:ApiKey"]!);

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    openAIClient.GetEmbeddingClient("text-embedding-3-small").AsEmbeddingGenerator());

builder.Services.AddSingleton<IChatClient>(
    openAIClient.GetChatClient("gpt-4o-mini").AsChatClient());
```

**Why `AddSingleton` and not `AddScoped`?**
The OpenAI client is stateless and thread-safe — one instance is fine for the whole app lifetime. Scoped would create a new client per request, which is wasteful.

### Model names to know
| Model | Use For | Notes |
|-------|---------|-------|
| `text-embedding-3-small` | Generating embeddings | 1,536 dimensions, matches your `vector(1536)` column |
| `gpt-4o-mini` | Chat responses | Fast and cheap, good for ops Q&A |
| `gpt-4o` | More complex reasoning | More expensive, use when answers need to be precise |

> ⚠️ `gpt-4o-mini` — the **o** is the letter O (for "omni"), not the number zero. `gpt-40-mini` will give a 404 error.

---

## Group 2: Knowledge Page with Auto-Embedding

### 🟢 Simple
When you save a knowledge entry, Sentinel secretly sends the content to OpenAI and gets back 1,536 numbers (the embedding). Those numbers are stored alongside the text. Think of it like filing a document AND stamping its GPS coordinates on the cover — later you can find it by location (meaning) instead of just by title.

### 🟡 Real — Generating an Embedding
```csharp
@inject IEmbeddingGenerator<string, Embedding<float>> EmbeddingGenerator

var result = await EmbeddingGenerator.GenerateAsync([_form.Content]);
var vector = result[0].Vector.ToArray();
```

- `GenerateAsync` takes a list of strings (you pass one)
- Returns a `GeneratedEmbeddings<Embedding<float>>` — index `[0]` for the first result
- `.Vector` is `ReadOnlyMemory<float>` — call `.ToArray()` to get a plain `float[]`
- That `float[]` goes straight into `KnowledgeEntry.Embedding`

### The `_saving` State Pattern
OpenAI calls take 1–2 seconds. Disable the submit button while waiting to prevent double-submits:

```csharp
private bool _saving = false;

private async Task HandleCreate()
{
    _saving = true;
    // ... async work ...
    _saving = false;
}
```

In the template:
```razor
<button type="submit" disabled="@_saving"
        class="... disabled:opacity-50">
    @(_saving ? "Saving…" : "Save Entry")
</button>
```

`disabled:opacity-50` is a Tailwind modifier — it applies `opacity-50` only when the button is disabled.

### The "indexed" badge
```razor
<span class="... @(entry.Embedding is not null ? "bg-ctp-teal text-ctp-base" : "bg-ctp-surface text-ctp-muted")">
    @(entry.Embedding is not null ? "indexed" : "no embedding")
</span>
```

Entries without embeddings can't be searched — the badge makes this visible at a glance.

---

## Group 3: Semantic Search

### 🟢 Simple
You ask "how do I fix a crashed app?" and Sentinel finds your runbook on "database restart procedure" — because the meanings are close on the embedding map. This is semantic search: finding by meaning, not by matching words.

### 🟡 Real — CosineDistance
pgvector gives you a `CosineDistance` function usable inside EF Core LINQ queries. Cosine distance measures the angle between two vectors:
- `0.0` = identical meaning
- `0.5` = somewhat related
- `1.0+` = mostly unrelated

```csharp
// In KnowledgeService.cs
using Pgvector;
using Pgvector.EntityFrameworkCore;

public async Task<List<KnowledgeEntry>> SearchAsync(float[] queryEmbedding, int limit = 5)
{
    var vector = new Vector(queryEmbedding);
    return await _db.KnowledgeEntries
        .Where(e => e.Embedding != null)
        .Where(e => e.Embedding!.CosineDistance(vector) < 0.7)
        .OrderBy(e => e.Embedding!.CosineDistance(vector))
        .Take(limit)
        .ToListAsync();
}
```

**Why the threshold matters:**
Without `.Where(e => e.Embedding!.CosineDistance(vector) < 0.7)`, the query returns the top N results even if they're completely unrelated — it just returns everything sorted by distance. The threshold filters out entries that are too far away to be useful.

**Tuning the threshold:**
| Value | Behavior |
|-------|----------|
| `< 0.4` | Very strict — only near-identical meaning |
| `< 0.5` | Strict — might miss related entries phrased differently |
| `< 0.7` | Good default — catches related entries, filters noise |
| `< 0.9` | Loose — almost everything shows up |

### Adding SearchAsync to the Interface
```csharp
// IKnowledgeService.cs
Task<List<KnowledgeEntry>> SearchAsync(float[] queryEmbedding, int limit = 5);
```

Always add new methods to the interface first, then implement — the compiler will warn you if the service doesn't implement the interface.

### Search UI Pattern (button-triggered)
```csharp
private string _searchQuery = string.Empty;
private List<KnowledgeEntry>? _searchResults = null;  // null = no active search
private bool _searching = false;

private async Task HandleSearch()
{
    if (string.IsNullOrWhiteSpace(_searchQuery)) return;
    _searching = true;
    var result = await EmbeddingGenerator.GenerateAsync([_searchQuery]);
    _searchResults = await KnowledgeService.SearchAsync(result[0].Vector.ToArray());
    _searching = false;
}

private void ClearSearch()
{
    _searchQuery = string.Empty;
    _searchResults = null;
}
```

**The `??` display pattern:**
```csharp
var displayList = _searchResults ?? _entries;
```
`_searchResults` is `null` when no search is active, so `??` falls back to showing all entries. Clean way to toggle between "all" and "search results" views.

---

### Side Note: Live Search (Search-as-You-Type)

If you want results to appear automatically as the user types (without clicking a Search button), use debouncing — wait until the user pauses typing for 500ms before firing the search. Calling OpenAI on every keystroke would be slow and expensive.

```razor
@implements IDisposable

<input @oninput="OnSearchInput" value="@_searchQuery" placeholder="Search by meaning…" ... />
```

```csharp
private CancellationTokenSource? _debounce;

private async Task OnSearchInput(ChangeEventArgs e)
{
    _searchQuery = e.Value?.ToString() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(_searchQuery))
    {
        ClearSearch();
        return;
    }

    _debounce?.Cancel();
    _debounce = new CancellationTokenSource();

    try
    {
        await Task.Delay(500, _debounce.Token);  // wait for user to pause
        await HandleSearch();
    }
    catch (TaskCanceledException) { }  // user typed again — this search was cancelled
}

public void Dispose() => _debounce?.Dispose();
```

**Key concepts:**
- `@oninput` fires on every keystroke (vs `@bind` which fires on blur)
- `CancellationTokenSource` cancels the pending delay when a new keystroke arrives
- `IDisposable` + `Dispose()` cleans up the token when the component is destroyed
- `TaskCanceledException` is expected and safe to swallow here

---

## Group 4: AI Chat Panel (RAG)

### 🟢 Simple
The right sidebar is now a real AI assistant. You type a question, it searches your knowledge base, hands the relevant entries to the AI, and the AI answers using your own documentation. The "Sources used" section shows exactly which entries it drew from — so you can verify the answer is grounded in your data.

### 🟡 Real — IChatClient
```csharp
var messages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.System, systemPrompt),
    new ChatMessage(ChatRole.User, _lastQuestion)
};

var response = await ChatClient.GetResponseAsync(messages);
_answer = response.Text ?? string.Empty;
```

- `ChatRole.System` sets the AI's behavior and provides context
- `ChatRole.User` is the actual question
- `GetResponseAsync` returns a `ChatResponse` with a `.Text` shortcut property

### Building the System Prompt
The system prompt is the secret ingredient — it tells the AI who it is, what it knows, and how to behave:

```csharp
var contextText = _context.Count > 0
    ? string.Join("\n\n", _context.Select(e => $"## {e.Title}\n{e.Content}"))
    : "No relevant knowledge base entries found.";

var systemPrompt = $"""
    You are an ops assistant for Sentinel, an infrastructure knowledge base.
    Answer the user's question based on the following knowledge base entries.
    If the answer isn't in the provided context, say so clearly. Be concise.

    {contextText}
    """;
```

The `"""..."""` syntax is a **raw string literal** — no need to escape quotes or newlines inside it. The `$` prefix makes it an interpolated string so you can use `{contextText}`.

### Component Architecture
You built `AiChat.razor` as a separate shared component, then dropped `<AiChat />` into `MainLayout.razor`. This is good separation — the layout doesn't know or care how the chat works.

```razor
@* MainLayout.razor — right sidebar *@
<div class="flex-1 overflow-hidden">
    <AiChat />
</div>
```

`overflow-hidden` on the container + `flex flex-col h-full` inside `AiChat` makes the component fill the sidebar and manage its own scroll.

### Full RAG Flow in Code
```csharp
private async Task HandleAsk()
{
    if (string.IsNullOrWhiteSpace(_question)) return;
    _thinking = true;
    _answer = null;
    _lastQuestion = _question;

    // Step 1: Retrieve — embed the question, search knowledge base
    var embeddings = await EmbeddingGenerator.GenerateAsync([_question]);
    _context = await KnowledgeService.SearchAsync(embeddings[0].Vector.ToArray(), limit: 3);

    // Step 2: Augment — build system prompt with retrieved context
    var contextText = _context.Count > 0
        ? string.Join("\n\n", _context.Select(e => $"## {e.Title}\n{e.Content}"))
        : "No relevant knowledge base entries found.";

    var systemPrompt = $"""
        You are an ops assistant for Sentinel, an infrastructure knowledge base.
        Answer the user's question based on the following knowledge base entries.
        If the answer isn't in the provided context, say so clearly. Be concise.

        {contextText}
        """;

    // Step 3: Generate — call the language model
    var messages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.System, systemPrompt),
        new ChatMessage(ChatRole.User, _lastQuestion)
    };

    var response = await ChatClient.GetResponseAsync(messages);
    _answer = response.Text ?? string.Empty;
    _question = string.Empty;
    _thinking = false;
}
```

---

## Packages Required (and where)

| Package | Project | Why |
|---------|---------|-----|
| `Microsoft.Extensions.AI.OpenAI` | Infrastructure + Web | Provides `IEmbeddingGenerator`, `IChatClient`, extension methods |
| `Pgvector` | Infrastructure | `Vector` type for pgvector conversion |
| `Pgvector.EntityFrameworkCore` | Infrastructure + Web | `CosineDistance` LINQ extension, `UseVector()` |

> Always check which project needs a package. Just because Infrastructure has it doesn't mean Web can use the extension methods — each project that *calls* the API needs its own reference.

---

## Troubleshooting

### `AsEmbeddingGenerator` / `AsChatClient` not recognized
The `Microsoft.Extensions.AI.OpenAI` package is missing from the project where you're calling the method. Add it:
```bash
dotnet add src/Sentinel.Web/Sentinel.Web.csproj package Microsoft.Extensions.AI.OpenAI
```

### `AsEmbeddingGenerator("model-name")` doesn't compile
In newer versions (10.x+), the API changed. You need to get a specific sub-client first:
```csharp
# Old (doesn't work in 10.x):
openAIClient.AsEmbeddingGenerator("text-embedding-3-small")

# New (correct):
openAIClient.GetEmbeddingClient("text-embedding-3-small").AsEmbeddingGenerator()
openAIClient.GetChatClient("gpt-4o-mini").AsChatClient()
```

### `CompleteAsync` not recognized
Renamed in newer versions — use `GetResponseAsync` instead. Also update the response access:
```csharp
# Old:
var response = await ChatClient.CompleteAsync(messages);
_answer = response.Message.Text ?? string.Empty;

# New:
var response = await ChatClient.GetResponseAsync(messages);
_answer = response.Text ?? string.Empty;
```

### HTTP 404: model_not_found — `gpt-40-mini`
Typo — the model name is `gpt-4o-mini` with the **letter o** (omni), not the number zero. Check `Program.cs`.

### `CosineDistance` not recognized in KnowledgeService
`Pgvector.EntityFrameworkCore` is missing from the Infrastructure project. Add it:
```bash
dotnet add src/Sentinel.Infrastructure/Sentinel.Infrastructure.csproj package Pgvector.EntityFrameworkCore
```

### Search returns unrelated results
Your threshold is too high. Tighten it in `SearchAsync`:
```csharp
.Where(e => e.Embedding!.CosineDistance(vector) < 0.7)  // try 0.5 or 0.6 if noise persists
```

### Search misses entries that should match
Your threshold is too strict. Loosen it:
```csharp
.Where(e => e.Embedding!.CosineDistance(vector) < 0.7)  // raise from 0.5 if entries are missing
```

---

## Phase 8 Concepts at a Glance

| Concept | Where Used |
|---------|-----------|
| Embeddings | 1,536 floats encoding text meaning, stored in `KnowledgeEntry.Embedding` |
| `IEmbeddingGenerator.GenerateAsync()` | Converts text → float[] via OpenAI API |
| `IChatClient.GetResponseAsync()` | Sends messages to GPT, gets answer |
| `ChatRole.System` / `ChatRole.User` | Structuring the conversation for the AI |
| `CosineDistance` | pgvector LINQ extension — measures meaning distance between vectors |
| Distance threshold | Filters irrelevant results — tune between 0.5–0.7 |
| RAG pattern | Retrieve → Augment → Generate |
| `_saving` / `_thinking` bool | Disable buttons + show feedback during async AI calls |
| Raw string literals `"""..."""` | Multi-line strings without escaping, used for system prompts |
| `AddSingleton` for AI clients | One shared instance — AI clients are stateless and thread-safe |
| User secrets | Safe local storage for API keys, never committed to git |
| Component composition | `AiChat.razor` as a separate component dropped into `MainLayout` |

---

## What You've Built: The Full Sentinel Stack

```
[Browser] → Blazor Server (SignalR circuit)
    → ASP.NET Core Identity (auth + cookies)
    → Clean Architecture layers:
        Web → Application (interfaces) → Infrastructure (implementations)
    → EF Core → PostgreSQL + pgvector extension
    → OpenAI API (embeddings + chat)
```

**Pages built:**
- `/login` — static SSR cookie auth
- `/register` — interactive server registration
- `/` — dashboard with live counts
- `/runbooks` — full CRUD with status tracking
- `/incidents` — full CRUD with severity/status color badges + Mark as Resolved
- `/knowledge` — auto-embedding on save, semantic search, threshold tuning

**AI sidebar:** RAG pipeline — embed question → vector search → grounded AI answer with sources

---

*Phase 8 complete. You now have a working AI-powered ops knowledge base. Possible next steps: Phase 9 (file upload — ingest PDFs/docs into the knowledge base), or Phase 10 (streaming AI responses so the answer appears word by word instead of all at once).*
