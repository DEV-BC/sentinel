# Phase 9 Study Guide: Document Upload & Ingestion

You built a full document pipeline: upload a PDF or Word file, extract the text, generate an embedding, and save it as a searchable knowledge entry. You also added inline expand/edit to knowledge cards.

---

## The Big Picture

```
[User picks file]
    → InputFile → IBrowserFile (filename, size, stream)
    → IBrowserFile.OpenReadStream() → Stream
    → IDocumentExtractor.ExtractTextAsync()
        → PdfPig (PDF) or DocumentFormat.OpenXml (Word)
        → plain text string
    → IEmbeddingGenerator.GenerateAsync() → float[1536]
    → KnowledgeService.CreateAsync() → PostgreSQL
    → entry appears in list with "indexed" badge
```

---

## Group 1: File Upload UI with InputFile

### 🟢 Simple
Like an email attachment button — the user clicks, picks a file, and the browser hands it to your code. Blazor's `InputFile` component wraps the native browser file picker.

### 🟡 Real
`InputFile` renders a standard HTML `<input type="file">`. When the user picks a file, `OnChange` fires with an `InputFileChangeEventArgs`. From that you get an `IBrowserFile` — a reference to the chosen file.

```razor
<InputFile OnChange="HandleFileSelected" accept=".pdf,.docx"
           class="text-ctp-muted text-sm" />
```

```csharp
private IBrowserFile? _selectedFile = null;

private void HandleFileSelected(InputFileChangeEventArgs e)
{
    _selectedFile = e.File;
}
```

### IBrowserFile properties
| Property | Type | What It Is |
|----------|------|-----------|
| `.Name` | `string` | Filename including extension |
| `.Size` | `long` | File size in bytes |
| `.ContentType` | `string` | MIME type (e.g. `application/pdf`) |
| `.OpenReadStream(maxAllowedSize)` | `Stream` | The actual file bytes |

### Displaying file size in KB
```razor
<p class="text-ctp-muted text-xs">@(Math.Round(_selectedFile.Size / 1024.0, 1)) KB</p>
```

`Math.Round(value, 1)` rounds to 1 decimal place. `1024.0` (not `1024`) forces floating-point division — integer division would drop the decimal.

### Mutual-exclusive toggle buttons
When "New Entry" is clicked, the upload panel closes, and vice versa:
```csharp
@onclick="() => { _showUpload = false; _showForm = !_showForm; }"
@onclick="() => { _showForm = false; _showUpload = !_showUpload; }"
```

The `{ }` block inside the lambda lets you run multiple statements in one `@onclick`.

---

## Group 2: IDocumentExtractor + PDF Extraction (PdfPig)

### 🟢 Simple
A PDF isn't just text — it's a complex format with fonts, positions, and rendering instructions. PdfPig speaks "PDF" and translates it to plain words your code can use. Think of it as a translator between two very different languages.

### 🟡 Real — Streams and MemoryStream
A `Stream` is a channel of bytes you read through, like water through a pipe. The browser delivers the file as a stream. PdfPig needs a **seekable** stream — one it can rewind and re-read from the beginning. Browser streams aren't seekable, so you copy the bytes into a `MemoryStream` (an in-memory buffer), reset its position to 0, and pass that to PdfPig.

```csharp
private static async Task<string> ExtractPdfAsync(Stream stream)
{
    using var ms = new MemoryStream();
    await stream.CopyToAsync(ms);   // pour all bytes into memory
    ms.Position = 0;                // rewind to the beginning

    using var pdf = PdfDocument.Open(ms);
    return string.Join("\n", pdf.GetPages()
        .Select(page => string.Join(" ", page.GetWords().Select(w => w.Text))));
}
```

**PdfPig's model:**
- `pdf.GetPages()` — every page in the document
- `page.GetWords()` — every word on that page with position data
- `w.Text` — the actual string content of the word

**`using var`** — automatically disposes (closes and frees) the `MemoryStream` and `PdfDocument` when the method exits, even if an exception is thrown. Always use `using` with streams and file handles.

### The Interface Pattern (same as Phase 6)
```csharp
// Sentinel.Application/IDocumentExtractor.cs
namespace Sentinel.Application;

public interface IDocumentExtractor
{
    Task<string> ExtractTextAsync(Stream stream, string fileName);
}
```

```csharp
// Sentinel.Infrastructure/Services/DocumentExtractorService.cs
public class DocumentExtractorService : IDocumentExtractor
{
    public async Task<string> ExtractTextAsync(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf"  => await ExtractPdfAsync(stream),
            ".docx" => await ExtractDocxAsync(stream),
            _       => throw new NotSupportedException($"File type '{ext}' is not supported.")
        };
    }
}
```

`Path.GetExtension("report.pdf")` → `".pdf"` — returns the extension including the dot.
`.ToLowerInvariant()` — makes the comparison case-insensitive (`".PDF"` becomes `".pdf"`).

Registration in `Program.cs`:
```csharp
builder.Services.AddScoped<IDocumentExtractor, DocumentExtractorService>();
```

---

## Group 3: Word Document (.docx) Extraction

### 🟢 Simple
A `.docx` file is actually a ZIP archive containing XML files. One of those XML files (`document.xml`) holds all the text as paragraphs. DocumentFormat.OpenXml reads that structure and hands you back the paragraphs directly.

### 🟡 Real
```csharp
private static async Task<string> ExtractDocxAsync(Stream stream)
{
    using var ms = new MemoryStream();
    await stream.CopyToAsync(ms);
    ms.Position = 0;

    using var doc = WordprocessingDocument.Open(ms, false);
    var paragraphs = doc.MainDocumentPart?.Document?.Body?.Elements<Paragraph>()
        ?? Enumerable.Empty<Paragraph>();
    return string.Join("\n", paragraphs
        .Select(p => p.InnerText)
        .Where(t => !string.IsNullOrWhiteSpace(t)));
}
```

**OpenXml document model:**
- `WordprocessingDocument` — the whole file
- `.MainDocumentPart` — the main content (vs headers, footers, etc.)
- `.Document.Body` — the main body text area
- `.Elements<Paragraph>()` — every paragraph in order
- `.InnerText` — all text inside the paragraph concatenated

`false` in `WordprocessingDocument.Open(ms, false)` means **read-only** — you're not editing the document.

**Why filter empty paragraphs?**
Word documents use blank paragraphs for visual spacing. `.Where(t => !string.IsNullOrWhiteSpace(t))` removes them so extracted text isn't full of empty lines.

### Null-safe chaining with `?.` and `??`
```csharp
doc.MainDocumentPart?.Document?.Body?.Elements<Paragraph>()
    ?? Enumerable.Empty<Paragraph>()
```

- `?.` — null-conditional: if any step is null, the whole chain returns null instead of throwing
- `??` — null-coalescing: if the left side is null, use the right side instead
- `Enumerable.Empty<Paragraph>()` — an empty collection, safe to pass to `string.Join`

This is safer than `doc.MainDocumentPart!.Document.Body!` which uses null-forgiving operators that suppress warnings but don't actually prevent crashes.

---

## Group 4: Full Upload Flow

### 🟢 Simple
All the pieces are built. This group wires them into one button click: pick file → extract text → generate embedding → save. Like an assembly line — each step hands its output to the next.

### 🟡 Real — Three New Concepts

**1. `OpenReadStream(maxAllowedSize)`**

`IBrowserFile` doesn't read itself — you explicitly open a stream with a size limit:
```csharp
var stream = _selectedFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
```
Blazor's default max is 512KB — too small for most PDFs. Setting `10 * 1024 * 1024` allows up to 10MB. Adjust for your use case.

**2. `Path.GetFileNameWithoutExtension()`**

Strips the extension for a clean auto-title:
```csharp
Path.GetFileNameWithoutExtension("database-runbook.pdf")  // → "database-runbook"
```

**3. `try / catch / finally`**

```csharp
try
{
    // happy path — runs if no errors
}
catch (Exception ex)
{
    // runs if anything in try throws
    _uploadError = $"Failed to process document: {ex.Message}";
}
finally
{
    // ALWAYS runs — success or failure
    _uploading = false;
}
```

`finally` guarantees cleanup even when things go wrong. Without it, if the extraction throws, `_uploading` would stay `true` forever and the button would be permanently disabled.

### Full Handler
```csharp
private async Task HandleUpload()
{
    if (_selectedFile is null) return;
    _uploading = true;
    _uploadError = string.Empty;

    try
    {
        var stream = _selectedFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        var text = await DocumentExtractor.ExtractTextAsync(stream, _selectedFile.Name);

        var result = await EmbeddingGenerator.GenerateAsync([text]);
        var vector = result[0].Vector.ToArray();

        await KnowledgeService.CreateAsync(new KnowledgeEntry
        {
            Title = Path.GetFileNameWithoutExtension(_selectedFile.Name),
            Content = text,
            Source = _selectedFile.Name,
            Embedding = vector,
        });

        _entries = await KnowledgeService.GetAllAsync();
        _selectedFile = null;
        _showUpload = false;
    }
    catch (Exception ex)
    {
        _uploadError = $"Failed to process document: {ex.Message}";
    }
    finally
    {
        _uploading = false;
    }
}
```

---

## Bonus: Inline Expand & Edit

You added click-to-expand cards with an inline edit form — no new page needed.

### The `_expandedId` pattern
```csharp
private Guid? _expandedId = null;

private void ToggleExpand(Guid id)
{
    _expandedId = _expandedId == id ? null : id;  // clicking same card collapses it
    _editingId = null;                             // always close edit when toggling
}
```

Storing a single `Guid?` (nullable) is cleaner than a `Dictionary<Guid, bool>` — only one card can be expanded at a time, and `null` means none.

### Variables inside `@foreach`
```razor
@foreach (var entry in displayList)
{
    var isExpanded = _expandedId == entry.Id;  // C# variable inside Razor foreach
    var isEditing = _editingId == entry.Id;

    <div class="@(isExpanded ? "..." : "...")">
```

You can declare C# variables inside a Razor `@foreach` block. They're scoped to each iteration, making the template logic readable without calling methods for every condition.

### `@onclick:stopPropagation`
```razor
<button @onclick="() => StartEdit(entry)" @onclick:stopPropagation="true">
    Edit
</button>
```

The Edit/Save/Cancel buttons sit inside the clickable card `<div>`. Without `stopPropagation`, clicking the button would also fire the card's `@onclick`, immediately collapsing the expansion. `stopPropagation` tells the event to stop at the button and not bubble up to parent elements.

### Plain HTML inputs with `@bind` (outside EditForm)
When you're not inside an `EditForm`, use standard HTML elements with `@bind`:
```razor
<input @bind="_editTitle" class="..." />
<textarea @bind="_editContent" rows="6" class="..."></textarea>
```

`@bind` on native HTML elements works exactly like `@bind-Value` on Blazor components — it sets the value and handles the `onchange` event. You don't need `EditForm` when you're not using DataAnnotations validation.

---

## Reference: Detail Page with Route Parameters

*You didn't build this in Phase 9 — the inline expand was used instead. This is here as a reference for future projects when you want a dedicated page per record.*

### What are route parameters?
Instead of `/knowledge`, a detail page lives at `/knowledge/{id}` where `{id}` is the entry's Guid. Clicking an entry navigates to its own URL — bookmarkable, shareable, browser-back-button friendly.

### How to build it

**Step 1** — create `src/Sentinel.Web/Components/Pages/KnowledgeDetail.razor`:
```razor
@page "/knowledge/{Id:guid}"
@rendermode InteractiveServer
@attribute [Authorize]
@inject IKnowledgeService KnowledgeService
@inject NavigationManager Nav

@if (_entry is null)
{
    <p class="text-ctp-muted">Loading…</p>
}
else
{
    <PageHeader Title="@_entry.Title" />
    <div class="bg-ctp-mantle border border-ctp-surface rounded-lg p-6 space-y-4">
        <p class="text-ctp-text text-sm whitespace-pre-wrap">@_entry.Content</p>
        @if (!string.IsNullOrEmpty(_entry.Source))
        {
            <p class="text-ctp-overlay text-xs">Source: @_entry.Source</p>
        }
        <button @onclick="() => Nav.NavigateTo("/knowledge")"
                class="text-ctp-muted text-sm hover:underline">
            ← Back to Knowledge Base
        </button>
    </div>
}

@code {
    [Parameter] public Guid Id { get; set; }
    private KnowledgeEntry? _entry;

    protected override async Task OnInitializedAsync()
    {
        _entry = await KnowledgeService.GetByIdAsync(Id);
    }
}
```

**Step 2** — link from the knowledge list (add to each card):
```razor
<a href="/knowledge/@entry.Id" class="text-xs text-ctp-mauve hover:underline">View</a>
```

### Key concepts for route parameters
- `@page "/knowledge/{Id:guid}"` — the `{Id:guid}` segment is a route parameter with a type constraint (`guid` validates it's a valid GUID)
- `[Parameter] public Guid Id { get; set; }` — Blazor automatically binds the URL segment to this property
- Type constraints: `:guid`, `:int`, `:bool`, `:datetime` — Blazor validates the URL and returns 404 if the type doesn't match
- `Nav.NavigateTo("/knowledge")` — programmatic navigation, same as clicking a link

---

## Packages Used in Phase 9

| Package | Project | What It Provides |
|---------|---------|-----------------|
| `PdfPig` | Infrastructure | PDF parsing — pages, words, text |
| `DocumentFormat.OpenXml` | Infrastructure | Word (.docx) parsing — paragraphs, inner text |

Both were installed back in Phase 3 — Phase 9 is where they finally get used.

---

## Troubleshooting

### Extracted text from PDF is garbled or missing words
PdfPig works best with text-based PDFs. Scanned PDFs (images of pages) have no selectable text — they need OCR (a separate tool). Check if you can select text in the PDF with your cursor — if not, it's a scanned image.

### `OpenReadStream` throws about file size
The file is larger than your `maxAllowedSize`. Increase the limit:
```csharp
_selectedFile.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024)  // 50MB
```

### Word doc extraction returns empty string
The document might have content in headers/footers or text boxes, not the main body. `Elements<Paragraph>()` on `Body` only gets main body paragraphs — headers, footers, and text boxes live elsewhere in the OpenXml structure.

### Upload works but entry shows "no embedding" badge
The embedding generation succeeded but the `Embedding` property wasn't mapped. Check that `OnModelCreating` in `AppDbContext` still has the `HasConversion` and `HasColumnType("vector(1536)")` setup from Phase 4.

### Click on card collapses immediately when clicking Edit button
You're missing `@onclick:stopPropagation="true"` on the Edit button. Without it, the click bubbles up to the card's `@onclick` and calls `ToggleExpand`, which collapses the card.

### `try/catch` swallows the error silently
Make sure `_uploadError` is bound in the template:
```razor
@if (!string.IsNullOrEmpty(_uploadError))
{
    <p class="text-ctp-red text-sm">@_uploadError</p>
}
```

---

## Phase 9 Concepts at a Glance

| Concept | Where Used |
|---------|-----------|
| `InputFile` / `IBrowserFile` | File picker component, file metadata |
| `OpenReadStream(maxAllowedSize)` | Reading file bytes as a stream |
| `MemoryStream` + `ms.Position = 0` | Making browser streams seekable |
| `using var` | Auto-disposing streams and file handles |
| PdfPig — `GetPages()` → `GetWords()` → `.Text` | PDF text extraction |
| DocumentFormat.OpenXml — `Elements<Paragraph>()` → `.InnerText` | Word text extraction |
| `Path.GetExtension().ToLowerInvariant()` | Routing by file type |
| `Path.GetFileNameWithoutExtension()` | Auto-title from filename |
| `try / catch / finally` | Error handling with guaranteed cleanup |
| `?.` null-conditional chain | Safe navigation through nullable properties |
| `_expandedId` (single Guid?) | Track which card is expanded |
| Variables inside `@foreach` | Per-iteration C# state in Razor template |
| `@onclick:stopPropagation` | Prevent click events from bubbling to parent |
| Plain HTML `<input @bind>` | Two-way binding outside of EditForm |
| Route parameters `{Id:guid}` | *(Reference)* Per-record detail pages |

---

*Phase 9 complete. Possible next steps: Phase 10 (streaming AI responses — answers appear word-by-word using IAsyncEnumerable), or Phase 11 (dashboard charts showing incident trends over time).*