# Contributing to BlazText

Thanks for considering a contribution! Issues, docs fixes, plugins, and features are all welcome.

## Prerequisites

- .NET SDK 10.0.300 or later (the repo targets `net10.0`)

## Build, test, run

```bash
dotnet build BlazText.slnx
dotnet test
dotnet run --project samples/BlazText.DemoApp    # demo at http://localhost:5024
dotnet pack src/BlazText -o artifacts            # or any other src project
```

## Repository layout

- `src/` — the five NuGet packages (see [docs/architecture.md](docs/architecture.md) for the dependency rules: `Models`/`Rendering` stay Blazor-free, core stays third-party-free, feature packages never reference each other)
- `samples/BlazText.DemoApp` — Blazor WASM demo/test host
- `tests/BlazText.Tests` — xunit + bUnit
- `docs/` — user & contributor documentation

## Making changes

1. Fork and branch from `main`.
2. Keep the package dependency rules intact; new heavy dependencies belong in a new feature package, not core.
3. Add or update tests. Component behavior that bUnit can't cover (caret handling, keyboard interception) should be verified manually in the demo app — note what you checked in the PR.
4. Update the relevant page under `docs/` when behavior or API surface changes.
5. `dotnet build BlazText.slnx` and `dotnet test` must pass.

## Manual verification checklist (JS-dependent paths)

Run the demo app and check whatever your change touches:

- Typing updates the bound value / document (Home page)
- `{{ u` opens autocomplete; arrows + Enter insert the drop (Kitchen sink)
- Search finds matches, highlights, and the Aa toggle changes results
- HTML source view round-trips content; validation badge reacts to broken markup
- E-mail preview shows Liquid-rendered, CSS-inlined output
- Save → New → Load restores content and images (Save & load page)

## Style

- Standard .NET conventions, `LangVersion latest`, nullable enabled.
- Public API gets XML docs where the name alone doesn't explain it.
- Plugin UI should use the shared `.blaztext-btn` / `.blaztext-input` classes and `--blaztext-*` variables so theming keeps working.

## Releases

Maintainers publish via `dotnet pack` from a tagged commit; package metadata lives in `Directory.Build.props`.
