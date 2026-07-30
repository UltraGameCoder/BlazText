# Theming

BlazText ships with a neutral look and two styling levers: **CSS custom properties** for colors/spacing/fonts, and **stable class names** for structural overrides. No theme engine, no build step — plain CSS, so it composes with AntBlazor, MudBlazor, Bootstrap, or your own design system.

## CSS custom properties

Set these on `.blaztext`, an ancestor, or `:root`:

| Property | Default | Controls |
| --- | --- | --- |
| `--blaztext-bg` | `#ffffff` | Editor & input background |
| `--blaztext-color` | `#1a1d21` | Text color |
| `--blaztext-border-color` | `#d0d5dd` | All borders |
| `--blaztext-radius` | `6px` | Outer corner radius |
| `--blaztext-font` | `system-ui, sans-serif` | Font family |
| `--blaztext-font-size` | `0.95rem` | Base font size |
| `--blaztext-mono-font` | `ui-monospace, monospace` | HTML source view |
| `--blaztext-min-height` / `--blaztext-max-height` | `10rem` / `none` | Surface size |
| `--blaztext-surface-padding` | `0.75rem` | Surface padding |
| `--blaztext-toolbar-bg` | `#f8f9fb` | Toolbar background |
| `--blaztext-toolbar-gap` / `--blaztext-toolbar-padding` | `0.25rem` / `0.375rem` | Toolbar layout |
| `--blaztext-panel-bg` | `#fcfcfd` | Panel background |
| `--blaztext-placeholder-color` | `#98a2b3` | Placeholder text |
| `--blaztext-muted-color` | `#667085` | Secondary text |
| `--blaztext-focus-color` | `#b4d0fe` | Focus ring |
| `--blaztext-btn-radius` | `4px` | Button corner radius |
| `--blaztext-btn-hover-bg` / `--blaztext-btn-active-bg` / `--blaztext-btn-active-border` | grays/blues | Button states |
| `--blaztext-highlight-bg` / `--blaztext-highlight-active-bg` | `#ffe58f` / `#ff9c6e` | Search match highlights |

Example — match an AntDesign-flavored app:

```css
.blaztext {
    --blaztext-border-color: #d9d9d9;
    --blaztext-radius: 2px;
    --blaztext-btn-active-bg: #e6f4ff;
    --blaztext-btn-active-border: #91caff;
    --blaztext-focus-color: #91caff;
}
```

## Class hooks

Structure uses stable, documented class names — target them from your own stylesheet (from a parent component, use `::deep`):

- `.blaztext`, `.blaztext-toolbar`, `.blaztext-toolbar-item`, `.blaztext-main`, `.blaztext-surface`, `.blaztext-panel`, `.blaztext-panel-right`, `.blaztext-panel-bottom`
- Building blocks plugins reuse: `.blaztext-btn` (+ `.active`), `.blaztext-input`, `.blaztext-muted`

The editor also forwards `Class`, `Style`, and any additional attributes to its root element:

```razor
<BlazTextEditor Class="my-editor" Style="max-width: 48rem" />
```

Plugin authors: build toolbar items from `.blaztext-btn` / `.blaztext-input` and your plugin automatically follows the consumer's theme.
