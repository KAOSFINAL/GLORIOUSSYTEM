# Navigation Lazy Loading Design

## Intent

Make the first screen and menu navigation responsive by avoiding construction of secondary pages during Shell startup.

## Goals

- Create only Dashboard during `AppShell` construction.
- Create Webcam, Reports, and Settings the first time they are selected.
- Preserve the existing cached-page behavior after first use.
- Keep the current routes, Shell items, page layouts, and navigation API unchanged.

## Non-goals

- No Shell route redesign.
- No visual redesign or animation changes.
- No changes to database queries, CNN initialization, or page internals.
- No new dependency or navigation abstraction.

## Chosen approach

Use the existing `EnsureContent(string route)` method as the lazy-loading boundary. Remove the post-load warm-up handler and its delayed page construction from `AppShell.xaml.cs`; `NavigateTo` already calls `EnsureContent` before changing `CurrentItem`, so each page will still be created once and retained by its existing `ShellContent`.

## Constraints

- Keep the current Dashboard-first startup behavior.
- Keep navigation targets and route names unchanged.
- Do not dispose or recreate cached pages.

## Acceptance criteria

1. `AppShell` creates `MainPage` at startup but does not construct `ReportsPage`, `SettingsPage`, or `WebcamPage` until their route is selected.
2. Selecting each secondary menu item still displays the correct page.
3. Returning to a previously opened page reuses its existing instance.
4. The Windows MAUI project builds successfully.
