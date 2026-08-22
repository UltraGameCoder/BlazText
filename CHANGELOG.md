# Changelog

Notable changes to BlazText. Versions come from git tags via MinVer; entries here
are grouped under the release that carries them.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Breaking changes are listed first and always with the reason — while the project
is at `0.x` they can land in a minor release, but they are never silent.

## [Unreleased]

### Changed (breaking)

- `RenderOptions.LiquidContext` is replaced by `LiquidContextFactory`
  (`Func<TemplateContext>?`), invoked once per render. A single shared context
  could not isolate overlapping renders: scope push/pop is stack discipline, and
  two renders in flight do not release in LIFO order, so one render's values
  reached another document. Build a new context inside the factory. (#7)
- Liquid member access defaults to registered types only. Templates read
  dictionaries, scalars, and the members of types passed to
  `RenderOptions.AllowMembersOf<T>()`; anything else renders empty. BlazText
  documents are authored by end users, and the previous default let a template
  walk from a drop into whatever the object graph reached. Use
  `AllowAllMembersUnsafe()` for fully trusted authors. (#15)
- `EditorApi.HighlightRangesAsync` returns `Task<int>` — the number of ranges
  that resolved — instead of `Task`, so a caller can tell that the DOM moved on
  rather than showing a match count nothing on screen corresponds to. (#4)
- `AutoCompletePlugin.MaxItems` throws `ArgumentOutOfRangeException` when set to
  zero or less, instead of silently never showing the popup. (#5)
- `ImagePlugin.MaxFileSizeBytes` defaults to `5_000_000` rather than
  `5 * 1024 * 1024`, alongside a move to decimal units in the message, so the
  number reads back as written. Files between 5,000,000 and 5,242,880 bytes are
  now rejected under the default. (#11)

### Added

- `RenderOptions.LiquidTemplateOptions`, `AllowMembersOf<T>()`,
  `AllowMembersOf(Type)` and `AllowAllMembersUnsafe()` for controlling what a
  template may read. `LiquidPlugin` and `EmailPreviewPlugin` take the same
  options so the editor preview and the backend agree. (#15, #14)
- `EmailPreviewPlugin.Drops`, so the layout's own drops resolve in the preview
  rather than only in the sent e-mail. (#14)
- `BlazTextImageUri.ReplaceReferences` and `BlazTextRenderer.ResolveImageReferences`
  are public, and the HTML preview now shares them with the renderer instead of
  carrying a second copy. (#6)

### Fixed

- The paste sanitizer and `HtmlTooling.Sanitize` accepted obfuscated script URLs
  (`java&#9;script:`, entity-encoded schemes, `vbscript:`, `data:text/html`) and
  missed URL-bearing attributes. Both now normalize the URL before matching and
  check `srcset`/`ping` per candidate rather than whole-value. (#1, #2)
- Sanitized markup could become live on insertion: `DOMParser` parses with
  scripting off while the live document has it on, and the two disagree about
  `<noscript>`. The paste path inserts sanitized nodes instead of re-parsing a
  string, and `HtmlTooling.Sanitize` parses with `IsScripting = true`. (#1, #2)
- `HtmlTooling.Sanitize` removed every `<meta>`, which broke the mobile e-mail
  preview by dropping `<meta name="viewport">`. Only `http-equiv` is removed now. (#2)
- An uploaded file name and its browser-supplied content type reached the image
  markup unescaped, so either could break out of the attribute it landed in. The
  name is HTML-encoded and the content type validated against the MIME grammar,
  at the upload guard and again in `EmbeddedImage.ToDataUri()`. (#3)
- Search highlights were shared between editors on a page, so a search in one
  cleared another's; ranges are now per editor and stay index-aligned. (#4)
- Autocomplete divided by zero on a non-positive `MaxItems`, and opened an
  invisible popup when there was no caret — which still intercepted keys, so
  Enter committed a suggestion the user never saw. (#5)
- An image id that prefixed another one resolved to the wrong image, including
  when the longer id was not registered at all. References are now extracted and
  looked up rather than string-replaced. (#6)
- HTML validation reported positions shifted by the parser wrapper, and could
  place an issue on a line it did not belong to; positions are derived from the
  parser offset and are relative to the supplied content. (#8)
- The editor leaked its `DotNetObjectReference` and JS module handle when
  teardown failed, and interop during plugin disposal could take down a
  disconnected circuit. (#9, #10)
- An image size limit under 1 MB reported as "0 MB" through integer division,
  and rounding could advertise a limit more permissive than the one enforced. (#11)
