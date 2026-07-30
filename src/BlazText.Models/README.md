# BlazText.Models

Data contracts for BlazText documents, with **zero dependencies** (no Blazor). Reference this from any C# backend to share a clean contract with your BlazText frontend:

- `BlazTextDocument` — the save/load unit: HTML content, embedded image blobs, detected Liquid drops, per-plugin state. Serializes with System.Text.Json.
- `EmbeddedImage`, `DetectedDrop`, `HtmlValidationResult`, `SearchOptions`.

Save an editor's document anywhere, load it back to restore the session, or feed it to `BlazText.Rendering` to produce final webpage/e-mail HTML.

Documentation: https://github.com/UltraGameCoder/BlazText
