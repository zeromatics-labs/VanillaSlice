# VanillaSlice Landing Page — Design

**Date:** 2026-06-29
**Status:** Approved design (pre-implementation)
**Author:** Brainstorming session

## Goal

Create a public, marketing-grade landing page for VanillaSlice so it can be hosted and
marketed for adoption. The page must:

- Drive visitors to the online project generator (the wizard) as the single primary action.
- Carry best-in-class SEO targeting *vertical slice architecture*, *clean architecture*,
  *free .NET templates*, and *Blazor / MAUI code generator*.
- Build trust by making the open, free, no-lock-in nature unmistakable: nothing financial,
  full source included, code-that-generates-code you own and freely tweak, with no dependency
  the project could monetize in future.

## Decisions (locked)

| Decision | Choice |
|----------|--------|
| Tech & hosting | One SSR Blazor page inside the existing VanillaStudio app (already hosted). |
| Route strategy | Replace `Home.razor` at `/` — `/` becomes the landing page; wizard stays at `/wizard`. |
| Primary CTA | "Generate Your Project" → `/wizard`. |
| Visual style | Clean .NET-modern (violet/purple palette, crisp cards, code samples). |
| Hero/OG imagery | CSS-styled "generated slice tree" code visual; placeholder `og-image.png` reference to swap later. |
| Canonical domain | `https://vanillaslice.dev` |
| GitHub link | `https://github.com/zero-know/VanillaSlice` |

## Why these decisions

- The root `/` route in VanillaStudio already renders **static SSR** by default
  (`App.razor` only switches to `InteractiveServer` when `HttpContext.AcceptsInteractiveRouting()`).
  Static SSR means crawlers receive the full marketed HTML in the initial response — no JS
  execution required — which is the core SEO win. Putting the landing page at `/` maximizes
  root-domain link equity.
- The app already loads Bootstrap 5 + bootstrap-icons via CDN and exposes `<HeadOutlet>` in
  `App.razor`, so per-page `<HeadContent>` SEO tags flow into `<head>`. No new CSS framework
  is introduced; the .NET-modern theme is a thin `landing.css` layered on Bootstrap.

## Architecture & files

**New / changed files (all under `src/VanillaStudio/`):**

- `Components/Pages/Home.razor` — **rewritten** as the landing page. `@page "/"`, static SSR,
  holds the `<HeadContent>` SEO block and composes the section components below.
- `Components/Pages/Landing/HeroSection.razor` — hero (headline, subhead, CTAs, code visual).
- `Components/Pages/Landing/FeatureCard.razor` — reusable presentational card
  (icon, title, body, optional status badge).
- `Components/Pages/Landing/StepCard.razor` — reusable "how it works" step card.
- `Components/Pages/Landing/FaqItem.razor` — reusable FAQ question/answer item.
- `wwwroot/css/landing.css` — Clean .NET-modern theme (palette, gradient hero, cards, layout).
  Referenced from `App.razor` `<head>` alongside existing stylesheets.
- `wwwroot/robots.txt` — allow all; points to sitemap.
- `wwwroot/sitemap.xml` — lists `/` and `/wizard` with canonical domain.
- `wwwroot/og-image.png` — **placeholder** referenced by OG/Twitter tags (real asset swapped later).

All landing components are pure presentational SSR — no `@rendermode`, no JS interop, no
interactivity. CTAs are plain anchor links to `/wizard` and the GitHub repo.

### Component boundaries

- `Home.razor` owns: page route, SEO head, page composition, section ordering, and the static
  content data (feature list, steps, FAQ entries) passed into the reusable components.
- `FeatureCard` / `StepCard` / `FaqItem` own: rendering one item from parameters. They depend on
  nothing but their parameters and Bootstrap/landing.css classes — understandable and changeable
  in isolation.
- `HeroSection` owns: the hero layout including the CSS code-tree visual and CTA buttons.

## SEO specification

Placed in `Home.razor` via `<PageTitle>` and `<HeadContent>`:

- **Title:** `VanillaSlice — Free Vertical Slice & Clean Architecture Templates for .NET (Blazor & MAUI)`
- **Meta description** (~155 chars): free, open-source code generator for .NET that scaffolds
  vertical-slice, clean-architecture features for Blazor Web and MAUI — full source, MIT, no lock-in.
- **Meta keywords** (included per request, low ranking weight): vertical slice architecture,
  clean architecture, .NET templates, Blazor templates, MAUI templates, free code generator,
  SOLID, open source, C#, CRUD scaffolding.
- **Canonical**: `https://vanillaslice.dev/`
- **robots** meta: `index, follow`.
- **Open Graph**: `og:type=website`, `og:title`, `og:description`, `og:url`,
  `og:image` (→ `/og-image.png`), `og:site_name`.
- **Twitter Card**: `summary_large_image` with title/description/image.
- **JSON-LD `SoftwareApplication`**: name, applicationCategory `DeveloperApplication`,
  operatingSystem `Windows, macOS, Linux`, `offers` price `0` USD, license `MIT`, sameAs GitHub URL.
- **JSON-LD `FAQPage`**: built from the FAQ section entries (enables FAQ rich results).
- **Semantic HTML**: exactly one `<h1>`, `<h2>` per section, descriptive alt/aria text,
  keyword-rich but natural copy.
- **`sitemap.xml`** + **`robots.txt`** in wwwroot.

## Page content (top → bottom)

1. **Top bar** — wordmark; anchor nav (Architecture, Features, FAQ, GitHub);
   primary "Generate Project" button.
2. **Hero** — H1: *"Vertical Slice + Clean Architecture for .NET — free, open, and yours to keep."*
   Subhead naming Blazor & MAUI. Primary CTA **Generate Your Project →** (`/wizard`),
   secondary **View Source on GitHub**. CSS "generated slice tree" code visual.
3. **Trust bar** — chips: `100% Free` · `MIT Licensed` · `Full Source Included` ·
   `No Runtime Dependency` · `Nothing to Monetize, Ever`.
4. **What & Why (Architecture)** — concise explainer of Vertical Slice + Clean Architecture; a
   slice owns UI → contract → domain → data end-to-end. Simple CSS/markup visual.
5. **How it works** — 3 `StepCard`s: Configure in the wizard → Generate complete solution →
   Own & tweak every line.
6. **Features grid** — `FeatureCard`s: Platforms (Blazor Web, MAUI Hybrid, MAUI Native),
   UI (Bootstrap 5, Fluent UI, Tailwind), Database (SQL Server +), Auth (Identity / JWT).
   Honest status badges matching README reality.
7. **"Code that generates code" / Confidence section** — directly states: no lock-in, no SDK
   dependency at runtime, no future paywall, MIT forever. The trust core.
8. **FAQ** — `FaqItem`s: Is it really free? · What is vertical slice architecture? · Do I depend
   on VanillaSlice at runtime? · Can I use the generated code commercially? · Will it ever be
   monetized? (mirrored into FAQ JSON-LD).
9. **Final CTA** — large "Generate your project now" → `/wizard`.
10. **Footer** — GitHub, Releases, License, docs links.

## Testing / verification

- `dotnet build` succeeds.
- `dotnet run`, then load `/`: page renders, all CTAs route to `/wizard`, GitHub links resolve.
- View raw page source (not DevTools DOM) and confirm `<title>`, meta description, OG, Twitter,
  and both JSON-LD blocks are present — proving SSR delivery, not client rendering.
- Validate JSON-LD against schema.org structure.
- Responsive check at mobile (~375px) and desktop widths.

## Out of scope (YAGNI)

- No blog, docs system, analytics, contact form, i18n, or newsletter.
- No interactivity / WASM on this page.
- No real OG image production (placeholder now; swap later).
- No changes to the wizard itself.
