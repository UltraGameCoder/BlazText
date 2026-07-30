# Save, load & rendering

## The document model

`BlazTextDocument` (in the Blazor-free `BlazText.Models` package) is the persistence contract between your frontend and backend:

```csharp
public class BlazTextDocument
{
    public int SchemaVersion { get; set; }
    public string Content { get; set; }                       // HTML, may contain Liquid + blaztext:{id} image refs
    public List<EmbeddedImage> Images { get; set; }           // image blobs travelling with the document
    public List<DetectedDrop> DetectedDrops { get; set; }     // which Liquid drops the author used
    public Dictionary<string, JsonElement> PluginState { get; set; }
}
```

It serializes cleanly with `System.Text.Json`. **Save** = serialize the bound document wherever you like (database, blob storage, localStorage). **Load** = deserialize and assign it back to the editor's `Document` parameter — the user continues exactly where they left off, images included.

Because `BlazText.Models` has zero dependencies, your ASP.NET Core backend references it without dragging in Blazor: a shared, distinct contract between frontend and backend.

## Embedded images

Inserted images are stored as blobs in `Images` and referenced in the HTML as `src="blaztext:{id}"` — the content stays small and diffable, and you decide at render time whether images become data URIs, CDN URLs, or `cid:` e-mail attachments. Use `DetectedDrop` the same way: inspect `doc.DetectedDrops` to know which values a template expects before you render or accept it.

## Rendering documents (webpages & e-mails)

`BlazText.Rendering` (also Blazor-free) turns a document into final HTML. The same pipeline runs inside the editor's previews and on your backend, so what the author saw is what you send:

```csharp
using BlazText.Rendering;

var options = RenderOptions.ForEmail();       // Liquid + images + CSS inlining
options.LiquidValues["user"] = new { name = "Ada" };
options.LayoutContent = layoutHtml;           // optional {{ body }} wrapper template

RenderResult result = await BlazTextRenderer.RenderAsync(document, options);
string finalHtml = result.Html;               // ready to send
```

The pipeline steps, each optional via `RenderOptions`:

1. **Liquid render** ([Fluid](https://github.com/sebastienros/fluid)) with your `LiquidValues`. Parse failures don't throw — the raw content is kept and a warning is added to `result.Warnings`.
2. **Layout wrapping** — `LayoutContent` is itself a Liquid template that receives the rendered content as `{{ body }}`. This is how an e-mail *layout* document wraps an e-mail *body* document.
3. **Image resolution** — `blaztext:{id}` references become data URIs by default, or whatever your `ImageResolver` returns (CDN upload, `cid:`, …).
4. **CSS inlining** ([PreMailer.Net](https://github.com/milkshakesoftware/PreMailer.Net)) — see below.

## Why CSS inlining for e-mail?

Most e-mail clients (Gmail, Outlook, …) strip `<style>` blocks and ignore external stylesheets; only inline `style=""` attributes survive reliably. BlazText's answer: **authors write normal CSS, inlining is a render step.**

- In the editor, authors keep classes and `<style>` blocks (via the HTML source view).
- `RenderOptions.ForEmail()` sets `InlineCss = true`; PreMailer computes each element's effective styles and writes them onto `style` attributes.
- The `EmailPreviewPlugin` runs the *same* option, so its preview shows post-inlining reality.
- For webpage output use `RenderOptions.ForWebPage()` — no inlining, your CSS remains untouched.
