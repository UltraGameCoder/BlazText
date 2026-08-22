# ImagePlugin

Package: `BlazText` · Namespace: `BlazText.Plugins`

Adds an insert-image toolbar button. Picked files are stored as blobs (`EmbeddedImage`) on the document and referenced in the HTML as `src="blaztext:{id}"` — the visible editor shows the image via a data URI, but the saved content stays blob-free.

Files are accepted only when the browser reports a well-formed `image/*` content type; anything else is rejected with "Not an image file.". A stored image whose content type is malformed renders as a generic binary type rather than being emitted into the HTML.

```razor
<BlazTextEditor @bind-Document="doc">
    <ImagePlugin MaxFileSizeBytes="2_000_000" ImageInserted="OnInserted" />
</BlazTextEditor>
```

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `MaxFileSizeBytes` | `long` | `5_000_000` (5 MB) | Size limit per file, in bytes. Enforced on the exact value; only the message shown to the user is rounded — down, to KB/MB/GB — so it never states a larger limit than is enforced. |
| `Accept` | `string` | `"image/*"` | File input accept filter |
| `Order` | `int` | `30` | Toolbar position |
| `ImageInserted` | `EventCallback<EmbeddedImage>` | — | Raised after insertion |

## Getting the blobs as a developer

The blobs travel with the document: `doc.Images` gives you id, filename, content type, and `byte[]` data (e.g. to upload to storage on save) — `Data.Length` is the image's exact size in bytes. At render time, `BlazText.Rendering` resolves `blaztext:{id}` references — to data URIs by default, or via your `RenderOptions.ImageResolver` (CDN URL, `cid:` attachment, …). HTML and e-mail previews resolve them automatically.
