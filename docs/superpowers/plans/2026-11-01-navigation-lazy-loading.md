# Navigation Lazy Loading Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Stop AppShell from eagerly constructing secondary pages during startup while preserving existing cached navigation.

**Architecture:** Keep the existing `AppShell.EnsureContent` and `NavigateTo` methods. Remove only the `Loaded` event warm-up path so secondary `ShellContent` instances are created on demand.

**Tech Stack:** .NET 10 MAUI, C#, ShellContent.

**Spec:** `docs/superpowers/specs/2026-11-01-navigation-lazy-loading-design.md`

## Global Constraints

- Do not redesign Shell navigation.
- Do not change page layouts or page internals.
- Do not add dependencies.
- Preserve cached page instances after first creation.

## Review Focus

- Startup must not construct secondary pages.
- First navigation to each secondary page must still create and display it.
- Repeated navigation must reuse the existing page instance.
- Current routes and Shell item selection must remain unchanged.
- The Windows MAUI build must pass.

### Task 1: Remove eager secondary-page warm-up

**Files:**
- Modify: `src/GLORIOUSSYSTEM.App/AppShell.xaml.cs`

**Interfaces:**
- Consumes: existing `EnsureContent(string route)` and `NavigateTo(string route)`.
- Produces: unchanged route and page-cache behavior with no startup warm-up.

- [ ] Remove the `Loaded` subscription, warm-up flag, and delayed `WarmSecondaryPagesAsync` method.
- [ ] Keep Dashboard construction and `EnsureContent` unchanged.
- [ ] Build with `dotnet build src/GLORIOUSSYSTEM.App/GLORIOUSSYSTEM.App.csproj -f net10.0-windows10.0.19041.0`.
- [ ] Inspect the diff to confirm only eager warm-up code was removed.
