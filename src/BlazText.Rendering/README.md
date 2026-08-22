# BlazText.Rendering

Turns a `BlazTextDocument` into final HTML — with **no Blazor dependency**, so the same pipeline runs in the editor's previews and on your backend:

1. **Liquid rendering** (Fluid) with your drops, including `{{ body }}` layout wrapping
2. **Embedded image resolution** (data URIs by default, or your own resolver)
3. **E-mail CSS inlining** (PreMailer.Net) — because e-mail clients strip `<style>` blocks

```csharp
var options = RenderOptions.ForEmail();
options.LiquidValues["user"] = recipient;
options.AllowMembersOf<Recipient>();
var result = await BlazTextRenderer.RenderAsync(document, options);
// result.Html is ready to send
```

Documents are Liquid templates written by whoever uses the editor, so member access is restricted by
default: templates read dictionaries and the types you allow, never the wider object graph a drop can
reach. `AllowAllMembersUnsafe()` opts out when authors are fully trusted.

Documentation: https://github.com/UltraGameCoder/BlazText
