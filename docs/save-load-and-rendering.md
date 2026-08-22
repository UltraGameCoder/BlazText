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
options.LiquidValues["user"] = recipient;     // your own drop type
options.AllowMembersOf<Recipient>();          // …whose members templates may read
options.LayoutContent = layoutHtml;           // optional {{ body }} wrapper template

RenderResult result = await BlazTextRenderer.RenderAsync(document, options);
string finalHtml = result.Html;               // ready to send
```

The pipeline steps, each optional via `RenderOptions`:

1. **Liquid render** ([Fluid](https://github.com/sebastienros/fluid)) with your `LiquidValues`. Parse failures don't throw — the raw content is kept and a warning is added to `result.Warnings`.
2. **Layout wrapping** — `LayoutContent` is itself a Liquid template that receives the rendered content as `{{ body }}`. This is how an e-mail *layout* document wraps an e-mail *body* document.
3. **Image resolution** — `blaztext:{id}` references become data URIs by default, or whatever your `ImageResolver` returns (CDN upload, `cid:`, …).
4. **CSS inlining** ([PreMailer.Net](https://github.com/milkshakesoftware/PreMailer.Net)) — see below.

## Template trust model

A BlazText document *is* a Liquid template, and it is written by whoever uses the editor. Rendering it runs their code against your objects, so the question "what may a template read?" is a real one — and BlazText answers it conservatively by default.

`RenderOptions.LiquidTemplateOptions` is a Fluid `TemplateOptions` configured with Fluid's **registered-members-only** access strategy. Out of the box a template can read:

- **plain values** — strings, numbers, dates, booleans;
- **dictionaries** — `IDictionary<string, object?>` resolves by key, no registration needed;
- **members of types you allow explicitly**, and nothing else.

```csharp
public sealed record Recipient(string Name, string Email);

var options = RenderOptions.ForEmail();
options.LiquidValues["user"] = recipient;
options.AllowMembersOf<Recipient>();          // {{ user.Name }} resolves
```

`AllowMembersOf<T>()` allows **that type only**. A member whose own type isn't allowed too renders as nil, so a drop is a leaf, not an entry point: hand over an EF entity and `{{ order.Reference }}` works while `{{ order.Customer.Context.Database… }}` stays empty. Allow each type you actually want readable.

Two things that catch people out:

- **Member names are matched exactly.** `{{ user.name }}` does not find a property called `Name`. Since Liquid authors write lowercase, set the naming strategy once: `options.LiquidTemplateOptions.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;`
- **Anonymous types need their runtime type registered**, because you can't name them: `options.AllowMembersOf(user.GetType())`. Prefer a named record or a dictionary.

Unresolvable members render as empty output rather than throwing — the same as any other undefined Liquid drop — so if a value silently disappears, an unregistered type is the first thing to check.

### When authors are fully trusted

If the only people who can author documents are as trusted as your own code (an internal tool with admin-only access, say), you can opt out:

```csharp
options.AllowAllMembersUnsafe();   // every public member, however deep the graph
```

This is Fluid's `UnsafeMemberAccessStrategy`. It removes the containment described above entirely: any object reachable from `LiquidValues` becomes readable, including the services, `DbContext`, and configuration an entity may drag along. It is a deliberate one-liner so it shows up in a code review — don't reach for it to make a drop work when `AllowMembersOf<T>()` is what you meant.

`LiquidTemplateOptions` is the full Fluid `TemplateOptions`, so untrusted-author setups can also bound the work a template may do — `MaxSteps`, `MaxRecursion`, `Filters`, `CultureInfo`, `TimeZone`. Unlimited `MaxSteps` (Fluid's default) means a hostile `{% for %}` can spin a render thread.

For complete control, set `RenderOptions.LiquidContext` to a `TemplateContext` you built yourself; `LiquidTemplateOptions` is then ignored and `LiquidValues` are applied on top of your context.

### Keep the editor preview and the backend in agreement

`LiquidPlugin` renders previews through the same pipeline with the same default, so pass it the same options instance your backend uses — otherwise a drop that resolves on the server renders empty in the preview, and the editor stops showing the truth:

```razor
<LiquidPlugin Drops="TemplateDrops.Values" LiquidTemplateOptions="TemplateDrops.Options" />
```

The `EmailRendering` and `KitchenSink` pages in `samples/BlazText.DemoApp` share one `TemplateDrops` class between preview and render for exactly this reason.

## Why CSS inlining for e-mail?

Most e-mail clients (Gmail, Outlook, …) strip `<style>` blocks and ignore external stylesheets; only inline `style=""` attributes survive reliably. BlazText's answer: **authors write normal CSS, inlining is a render step.**

- In the editor, authors keep classes and `<style>` blocks (via the HTML source view).
- `RenderOptions.ForEmail()` sets `InlineCss = true`; PreMailer computes each element's effective styles and writes them onto `style` attributes.
- The `EmailPreviewPlugin` runs the *same* option, so its preview shows post-inlining reality.
- For webpage output use `RenderOptions.ForWebPage()` — no inlining, your CSS remains untouched.
