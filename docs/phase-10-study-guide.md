# Phase 10 Study Guide: Streaming AI Responses

You upgraded the AI chat panel from "wait then dump" to a real streaming conversation with typing animation, multi-turn memory, and auto-scroll. This is the pattern used by every modern AI chat interface.

---

## The Big Picture

```
Before Phase 10:
  [User asks] → [5 seconds silence] → [Full answer appears]

After Phase 10:
  [User asks] → [Thinking…] → [word▌] → [word word▌] → [full answer + sources]
                                ↑ streaming cursor animates while tokens arrive
```

```
Multi-turn conversation flow:
  Turn 1: [system prompt + RAG context] + [Q1] → answer1
  Turn 2: [system prompt + RAG context] + [Q1] + [A1] + [Q2] → answer2
  Turn 3: [system prompt + RAG context] + [Q1] + [A1] + [Q2] + [A2] + [Q3] → answer3
```

---

## Group 1: IAsyncEnumerable + await foreach + StateHasChanged

### 🟢 Simple
Instead of waiting for the full answer then showing it, you receive it one word (token) at a time. `IAsyncEnumerable` is a sequence that produces items asynchronously over time — like a live ticker rather than a batch download.

### 🟡 Real

**`IAsyncEnumerable<T>`**
Regular `IEnumerable<T>` has all items ready upfront. `IAsyncEnumerable<T>` produces items one by one, each arriving at its own time. You consume it with `await foreach`:

```csharp
await foreach (var update in ChatClient.GetStreamingResponseAsync(messages))
{
    _currentAnswer += update.Text;
    StateHasChanged();
}
```

Each `update` is a chunk of text (one or a few tokens). `.Text` is the new content in this chunk.

**`StateHasChanged()`**
Normally Blazor re-renders when an event handler **completes**. But inside a streaming loop, you're updating `_currentAnswer` many times before the method finishes. `StateHasChanged()` tells Blazor "re-render **right now** — don't wait." Without it the full answer would appear all at once at the very end, defeating the purpose of streaming.

**`_streaming` state flag**
```csharp
_streaming = true;

await foreach (var update in ChatClient.GetStreamingResponseAsync(messages))
{
    _currentAnswer += update.Text;
    StateHasChanged();
}

_streaming = false;
```

`_streaming` tells the UI the stream is in-flight. The button is disabled while `_thinking || _streaming`, preventing double-submits.

### Method name note (Microsoft.Extensions.AI 10.x)
| Old name | New name |
|----------|----------|
| `CompleteAsync` | `GetResponseAsync` |
| `CompleteStreamingAsync` | `GetStreamingResponseAsync` |

If a streaming method doesn't compile, try the `Get`-prefixed variant.

---

## Group 2: Streaming Cursor Animation

### 🟢 Simple
A blinking `▌` at the end of the in-progress answer tells the user "more is coming." Without it, partial answers look like the AI stopped mid-thought.

### 🟡 Real
Tailwind's `animate-pulse` applies a CSS opacity fade loop — no custom CSS needed. The cursor `<span>` is conditionally rendered right after `@_currentAnswer` with no whitespace between them:

```razor
<p class="text-ctp-text text-sm">
    @_currentAnswer@if (_streaming)
    {<span class="animate-pulse text-ctp-mauve">▌</span>}
</p>
```

**Why the tight layout matters:** A newline or space between `@_currentAnswer` and the `@if` would add a visible gap before the cursor. The cursor must hug the last character, so the `@if` starts on the same line immediately after `@_currentAnswer`.

**`▌`** is Unicode character U+258C — a left half-block, commonly used as a terminal cursor.

When `_streaming` becomes `false`, the `<span>` disappears from the DOM automatically.

---

## Group 3: Multi-Turn Conversation History

### 🟢 Simple
Right now the AI forgets everything after each answer. Multi-turn means the AI sees the full conversation — every Q&A pair — on every new question. "What about the second one?" now makes sense because the AI remembers "the second one."

### 🟡 Real — Two New Concepts

**1. `record` type**

```csharp
private record Exchange(string Question, string Answer);
```

A `record` is a concise C# class optimized for storing data. This one line creates a type with two read-only properties, a constructor, equality comparison, and a readable `ToString()`. Equivalent to writing a full class with all of that manually. Use `record` when you just need to bundle data together immutably.

**2. Rebuilding the message list each turn**

The AI API expects a flat list of all messages. You reconstruct it from scratch every time:

```csharp
var messages = new List<ChatMessage> { new ChatMessage(ChatRole.System, systemPrompt) };
foreach (var ex in _history)
{
    messages.Add(new ChatMessage(ChatRole.User, ex.Question));
    messages.Add(new ChatMessage(ChatRole.Assistant, ex.Answer));
}
messages.Add(new ChatMessage(ChatRole.User, _currentQuestion));
```

The system prompt goes first every time (with fresh RAG context). Then all previous exchanges. Then the new question. The AI sees the full conversation and can reference anything in it.

### State design: completed vs in-progress

```csharp
private List<Exchange> _history = [];      // completed exchanges — rendered in full
private string _currentQuestion = string.Empty;  // question being processed
private string _currentAnswer = string.Empty;    // streaming in-progress
```

Splitting completed vs in-progress makes streaming clean. `_currentAnswer` gets appended on every token. When streaming finishes, it's moved into `_history` and cleared:

```csharp
_history.Add(new Exchange(_currentQuestion, _currentAnswer));
_currentQuestion = string.Empty;
_currentAnswer = string.Empty;
_streaming = false;
```

### Template pattern
```razor
@foreach (var exchange in _history)          // all completed exchanges
{
    <div>You: @exchange.Question</div>
    <div>AI: @exchange.Answer</div>
}

@if (!string.IsNullOrEmpty(_currentQuestion))  // current question (while AI thinks)
{
    <div>You: @_currentQuestion</div>
}

@if (_streaming || !string.IsNullOrEmpty(_currentAnswer))  // streaming answer
{
    <div>AI: @_currentAnswer ▌</div>
}
```

---

## Group 4: Clear Button + Auto-Scroll (JS Interop)

### 🟢 Simple
Two polish features: a "Clear" button to wipe the conversation, and auto-scroll so you never have to scroll down to see the new answer.

### 🟡 Real — Two New Concepts

**1. `@ref` — C# handle to a DOM element**

```razor
<div class="flex-1 overflow-y-auto ..." @ref="_chatContainer">
```

```csharp
private ElementReference _chatContainer;
```

`@ref` tells Blazor to populate `_chatContainer` with a reference to that specific DOM element. It's Blazor's equivalent of `document.getElementById()`. You can pass `ElementReference` to JavaScript interop calls.

**2. `IJSRuntime` — calling JavaScript from C#**

```csharp
@inject IJSRuntime JS

await JS.InvokeVoidAsync("scrollToBottom", _chatContainer);
```

`InvokeVoidAsync("functionName", args)` calls a JavaScript function by name and passes arguments. The function must exist in global scope (on `window`). This is called **JS interop** — Blazor reaching into the browser's JS engine for things C# can't do directly, like scrolling a DOM element.

The JS function (added to `App.razor`):
```html
<script>
    window.scrollToBottom = (el) => { if (el) el.scrollTop = el.scrollHeight; };
</script>
```

`el.scrollTop = el.scrollHeight` scrolls an element to its maximum scroll position (the bottom).

**When to scroll:**
```csharp
// 1. Immediately when user sends a question (scroll question into view)
StateHasChanged();
await JS.InvokeVoidAsync("scrollToBottom", _chatContainer);

// 2. When streaming completes (scroll full answer into view)
StateHasChanged();
await JS.InvokeVoidAsync("scrollToBottom", _chatContainer);
```

Scrolling on every token would be expensive. Scrolling at these two moments gives the right feel without hammering the browser.

### Clear conversation
```csharp
private void ClearConversation()
{
    _history.Clear();
    _currentQuestion = string.Empty;
    _currentAnswer = string.Empty;
    _context.Clear();
}
```

Resetting all state fields returns the component to its initial state — the placeholder reappears. The button only renders when `_history.Any()` so it doesn't show on a fresh chat.

---

## JS Interop Patterns (Reference)

| Use case | Method | Returns |
|----------|--------|---------|
| Call JS, ignore return | `InvokeVoidAsync("fn", args)` | `ValueTask` |
| Call JS, get a value back | `InvokeAsync<T>("fn", args)` | `ValueTask<T>` |
| Where to put JS functions | `<script>` in `App.razor` before `</body>` | global `window.*` |

Common uses for JS interop in Blazor:
- Scrolling elements
- Focusing an input programmatically
- Copying text to clipboard
- Reading browser APIs (window size, local storage)
- Integrating third-party JS libraries

---

## Troubleshooting

### `GetStreamingResponseAsync` not recognized
Make sure `@using Microsoft.Extensions.AI` is at the top of the component (or in `_Imports.razor`). If still failing, check that `Microsoft.Extensions.AI.OpenAI` package is installed in the Web project.

### Streaming appears but cursor doesn't animate
Check that the `@if (_streaming)` and `<span class="animate-pulse ...">` are on the same line as `@_currentAnswer` with no whitespace gap between them.

### `StateHasChanged()` throws about wrong thread
You're calling it from a timer or background thread outside the component's synchronization context. Use `await InvokeAsync(StateHasChanged)` instead — it marshals the call back to the component's render thread.

### Auto-scroll doesn't work
Confirm the `<script>` block is in `App.razor` and the function is on `window` (`window.scrollToBottom = ...`). Also confirm `@ref="_chatContainer"` is on the scrollable `<div>` (the one with `overflow-y-auto`), not a parent container.

### Multi-turn: AI doesn't remember previous answers
Check that `_history` is being populated at the end of `HandleAsk`:
```csharp
_history.Add(new Exchange(_currentQuestion, _currentAnswer));
```
And that the foreach loop building `messages` adds both the user and assistant messages from each exchange.

### Clear button doesn't reset the placeholder text
The placeholder condition checks multiple fields:
```csharp
!_history.Any() && string.IsNullOrEmpty(_currentQuestion) && !_thinking
```
Make sure `ClearConversation()` clears all of them, including `_currentQuestion`.

---

## Phase 10 Concepts at a Glance

| Concept | Where Used |
|---------|-----------|
| `IAsyncEnumerable<T>` | Streaming chat response — tokens arrive one by one |
| `await foreach` | Consuming a streaming response |
| `StateHasChanged()` | Force re-render mid-handler while streaming |
| `GetStreamingResponseAsync` | M.E.AI 10.x streaming method (was `CompleteStreamingAsync`) |
| `_streaming` bool | Track stream in-flight — disables button, shows cursor |
| `animate-pulse` | Tailwind CSS animation for blinking cursor |
| `▌` block cursor | Unicode U+258C — visual typing indicator |
| `record Exchange(...)` | Concise immutable data type for Q&A pairs |
| Multi-turn message list | Rebuild full history on every question for AI context |
| Completed vs in-progress state | `_history` for done, `_currentAnswer` for streaming |
| `@ref` | C# handle to a DOM element |
| `IJSRuntime.InvokeVoidAsync` | Call a JavaScript function from C# (JS interop) |
| `el.scrollTop = el.scrollHeight` | JavaScript to scroll an element to the bottom |

---

*Phase 10 complete. Two phases remaining:*
*Phase 11 — Dashboard (real stats: incident counts by severity, recent activity feed)*
*Phase 12 — Self-hosted deployment (Docker Compose, production config, team access via IP)*
*Phase 13 — Azure deployment (App Service, managed database, GitHub Actions CI/CD)*