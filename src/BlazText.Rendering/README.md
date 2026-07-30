# BlazText.Rendering

Turns a `BlazTextDocument` into final HTML — with **no Blazor dependency**, so the same pipeline runs in the editor's previews and on your backend:

1. **Liquid rendering** (Fluid) with your drops, including `{{ body }}` layout wrapping
2. **Embedded image resolution** (data URIs by default, or your own resolver)
3. **E-mail CSS inlining** (PreMailer.Net) — because e-mail clients strip `<style>` blocks

```csharp
var options = RenderOptions.ForEmail();
options.LiquidValues["user"] = new { name = "Ada" };
var result = await BlazTextRenderer.RenderAsync(document, options);
// result.Html is ready to send
```

Documentation: https://github.com/mikedegroot/BlazText
