# VanillaSlice Landing Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the `/` route of the VanillaStudio Blazor app with a marketing-grade, SEO-rich landing page that drives visitors to the project-generator wizard.

**Architecture:** A single static-SSR Blazor page (`Home.razor` at `/`) composed of small, purely-presentational section components, themed with a thin `landing.css` layered on the app's existing Bootstrap 5. All SEO (title, meta, Open Graph, Twitter, JSON-LD) is emitted server-side via `<HeadContent>` so crawlers receive complete HTML. Crawler assets (`robots.txt`, `sitemap.xml`) live in `wwwroot`.

**Tech Stack:** .NET 9, Blazor Web App (static SSR render mode), Bootstrap 5 + bootstrap-icons (already loaded via CDN), plain CSS, `System.Text.Json` for JSON-LD generation.

## Global Constraints

- Target framework: **.NET 9.0** (the VanillaStudio project; do not retarget).
- **No new NuGet packages, no new CSS/JS frameworks.** Reuse the Bootstrap 5 + bootstrap-icons already referenced in `App.razor`.
- The landing page and all its components are **static SSR**: no `@rendermode`, no `@onclick`, no JS interop, no `OnInitializedAsync`/state. CTAs are plain `<a>` anchors.
- Primary CTA target: **`/wizard`** (the existing wizard route — do not modify the wizard).
- Canonical domain (verbatim in all SEO tags/sitemap): **`https://vanillaslice.dev`**
- GitHub repo URL (verbatim in all source links): **`https://github.com/zero-know/VanillaSlice`**
- Visual direction: **Clean .NET-modern** — violet/purple palette (.NET brand purple `#512BD4`), crisp cards, generous spacing.
- New components live under `src/VanillaStudio/Components/Pages/Landing/`.
- TDD note: this is a presentational static page; the verification loop is `dotnet build` + an HTTP view-source assertion against the rendered `/` response (no bUnit/unit tests — they would be tautological for static markup).

---

## File Structure

| File | Responsibility |
|------|----------------|
| `src/VanillaStudio/wwwroot/css/landing.css` | Clean .NET-modern theme: palette vars, hero, trust bar, cards, code window, FAQ, footer, responsiveness. |
| `src/VanillaStudio/Components/App.razor` (modify) | Add `<link>` for `landing.css`. |
| `src/VanillaStudio/wwwroot/robots.txt` | Allow all crawlers; declare sitemap. |
| `src/VanillaStudio/wwwroot/sitemap.xml` | Canonical URL list (`/`, `/wizard`). |
| `src/VanillaStudio/wwwroot/og-image.png` | Placeholder social-share image (copied from favicon). |
| `src/VanillaStudio/Components/Pages/Landing/FeatureCard.razor` | Render one feature card (icon, title, body, optional status badge). |
| `src/VanillaStudio/Components/Pages/Landing/StepCard.razor` | Render one "how it works" step card. |
| `src/VanillaStudio/Components/Pages/Landing/FaqItem.razor` | Render one collapsible FAQ entry (`<details>`, no JS). |
| `src/VanillaStudio/Components/Pages/Landing/HeroSection.razor` | Hero: headline, subhead, CTAs, CSS code-window visual. |
| `src/VanillaStudio/Components/Pages/Home.razor` (rewrite) | `@page "/"`; SEO `<HeadContent>` (meta + OG + Twitter + JSON-LD), page composition, static content data. |

---

## Task 1: Clean .NET-modern theme stylesheet

**Files:**
- Create: `src/VanillaStudio/wwwroot/css/landing.css`
- Modify: `src/VanillaStudio/Components/App.razor` (add stylesheet link in `<head>`)

**Interfaces:**
- Consumes: nothing.
- Produces: CSS classes used by all later tasks — `vs-hero`, `vs-eyebrow`, `vs-hero-title`, `vs-hero-sub`, `vs-hero-cta`, `vs-btn-primary`, `vs-btn-ghost`, `vs-code-window`, `vs-code-bar`, `vs-code-body`, `vs-section`, `vs-section-alt`, `vs-section-head`, `vs-kicker`, `vs-h2`, `vs-lead`, `vs-trustbar`, `vs-chip`, `vs-feature-card`, `vs-feature-icon`, `vs-feature-head`, `vs-feature-title`, `vs-feature-body`, `vs-badge`, `vs-badge-done`, `vs-badge-wip`, `vs-badge-soon`, `vs-step-card`, `vs-step-number`, `vs-step-icon`, `vs-step-title`, `vs-step-body`, `vs-confidence`, `vs-confidence-list`, `vs-faq`, `vs-faq-item`, `vs-faq-q`, `vs-faq-a`, `vs-finalcta`, `vs-topbar`, `vs-wordmark`, `vs-topnav`, `vs-footer`, `vs-footer-links`.

- [ ] **Step 1: Create the stylesheet**

Create `src/VanillaStudio/wwwroot/css/landing.css`:

```css
/* VanillaSlice landing page — Clean .NET-modern theme (layered on Bootstrap 5) */
:root {
    --vs-primary: #512BD4;        /* .NET brand purple */
    --vs-primary-2: #7B5BE0;
    --vs-primary-d: #3A1FA0;
    --vs-ink: #14122B;
    --vs-body: #45415e;
    --vs-muted: #6c6a82;
    --vs-bg: #ffffff;
    --vs-bg-alt: #f6f4fd;
    --vs-line: #e7e3f5;
    --vs-code-bg: #1e1b35;
    --vs-code-ink: #e8e6ff;
    --vs-radius: 16px;
    --vs-shadow: 0 12px 40px rgba(81, 43, 212, 0.10);
    --vs-shadow-sm: 0 4px 18px rgba(20, 18, 43, 0.06);
}

.vs-page { color: var(--vs-body); background: var(--vs-bg); }
.vs-page h1, .vs-page h2, .vs-page h3 { color: var(--vs-ink); font-weight: 800; letter-spacing: -0.02em; }
.vs-page a { text-decoration: none; }

/* ---------- Top bar ---------- */
.vs-topbar {
    display: flex; align-items: center; justify-content: space-between;
    padding: 1rem 0; max-width: 1140px; margin: 0 auto; padding-left: 1rem; padding-right: 1rem;
}
.vs-wordmark { font-weight: 900; font-size: 1.3rem; color: var(--vs-ink); display: inline-flex; align-items: center; gap: .5rem; }
.vs-wordmark i { color: var(--vs-primary); }
.vs-topnav { display: flex; align-items: center; gap: 1.5rem; }
.vs-topnav a { color: var(--vs-body); font-weight: 600; }
.vs-topnav a:hover { color: var(--vs-primary); }

/* ---------- Buttons ---------- */
.vs-btn-primary {
    background: linear-gradient(135deg, var(--vs-primary), var(--vs-primary-2));
    color: #fff; border: none; font-weight: 700; border-radius: 999px;
    padding: .75rem 1.6rem; box-shadow: 0 8px 24px rgba(81,43,212,.35);
}
.vs-btn-primary:hover { color: #fff; filter: brightness(1.07); }
.vs-btn-ghost {
    background: #fff; color: var(--vs-ink); border: 1.5px solid var(--vs-line);
    font-weight: 700; border-radius: 999px; padding: .75rem 1.6rem;
}
.vs-btn-ghost:hover { border-color: var(--vs-primary); color: var(--vs-primary); }

/* ---------- Hero ---------- */
.vs-hero {
    background: radial-gradient(1200px 500px at 80% -10%, rgba(123,91,224,.18), transparent 60%),
                linear-gradient(180deg, var(--vs-bg-alt), var(--vs-bg));
    padding: 3.5rem 0 4.5rem;
}
.vs-eyebrow {
    display: inline-block; font-weight: 700; font-size: .8rem; letter-spacing: .08em; text-transform: uppercase;
    color: var(--vs-primary); background: rgba(81,43,212,.08); border: 1px solid rgba(81,43,212,.16);
    padding: .35rem .8rem; border-radius: 999px; margin-bottom: 1.1rem;
}
.vs-hero-title { font-size: clamp(2.1rem, 4.4vw, 3.4rem); line-height: 1.08; margin-bottom: 1.1rem; }
.vs-hero-sub { font-size: 1.18rem; color: var(--vs-body); max-width: 38ch; margin-bottom: 1.8rem; }
.vs-hero-cta { display: flex; flex-wrap: wrap; gap: .8rem; }

/* ---------- Code window visual ---------- */
.vs-code-window { background: var(--vs-code-bg); border-radius: var(--vs-radius); box-shadow: var(--vs-shadow); overflow: hidden; }
.vs-code-bar { display: flex; gap: .5rem; padding: .9rem 1rem; background: rgba(255,255,255,.04); border-bottom: 1px solid rgba(255,255,255,.06); }
.vs-code-bar span { width: 12px; height: 12px; border-radius: 50%; background: #ff5f57; }
.vs-code-bar span:nth-child(2) { background: #febc2e; }
.vs-code-bar span:nth-child(3) { background: #28c840; }
.vs-code-body { margin: 0; padding: 1.4rem 1.5rem; color: var(--vs-code-ink); font-size: .86rem; line-height: 1.55;
    font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace; white-space: pre; overflow-x: auto; }

/* ---------- Sections ---------- */
.vs-section { padding: 4.5rem 0; }
.vs-section-alt { background: var(--vs-bg-alt); }
.vs-section-head { max-width: 720px; margin: 0 auto 2.8rem; text-align: center; }
.vs-kicker { font-weight: 700; color: var(--vs-primary); text-transform: uppercase; letter-spacing: .08em; font-size: .82rem; }
.vs-h2 { font-size: clamp(1.7rem, 3vw, 2.4rem); margin: .5rem 0 .8rem; }
.vs-lead { font-size: 1.1rem; color: var(--vs-muted); }

/* ---------- Trust bar ---------- */
.vs-trustbar { display: flex; flex-wrap: wrap; justify-content: center; gap: .7rem; padding: 1.6rem 1rem; }
.vs-chip { display: inline-flex; align-items: center; gap: .45rem; font-weight: 700; color: var(--vs-ink);
    background: #fff; border: 1px solid var(--vs-line); border-radius: 999px; padding: .55rem 1.1rem; box-shadow: var(--vs-shadow-sm); }
.vs-chip i { color: var(--vs-primary); }

/* ---------- Feature cards ---------- */
.vs-feature-card { background: #fff; border: 1px solid var(--vs-line); border-radius: var(--vs-radius);
    padding: 1.6rem; box-shadow: var(--vs-shadow-sm); transition: transform .15s ease, box-shadow .15s ease; }
.vs-feature-card:hover { transform: translateY(-4px); box-shadow: var(--vs-shadow); }
.vs-feature-icon { width: 48px; height: 48px; border-radius: 12px; display: grid; place-items: center; font-size: 1.4rem;
    color: var(--vs-primary); background: rgba(81,43,212,.10); margin-bottom: 1rem; }
.vs-feature-head { display: flex; align-items: center; gap: .6rem; margin-bottom: .5rem; }
.vs-feature-title { font-size: 1.15rem; margin: 0; }
.vs-feature-body { color: var(--vs-muted); margin: 0; }
.vs-badge { font-size: .68rem; font-weight: 800; text-transform: uppercase; letter-spacing: .04em; padding: .2rem .55rem; border-radius: 999px; }
.vs-badge-done { color: #0a7d33; background: #e3f7ea; }
.vs-badge-wip { color: #9a6700; background: #fdf3d7; }
.vs-badge-soon { color: #5b5b6b; background: #ececf2; }

/* ---------- Step cards ---------- */
.vs-step-card { background: #fff; border: 1px solid var(--vs-line); border-radius: var(--vs-radius); padding: 1.8rem; height: 100%; position: relative; box-shadow: var(--vs-shadow-sm); }
.vs-step-number { position: absolute; top: -16px; left: 1.8rem; width: 34px; height: 34px; border-radius: 50%;
    background: linear-gradient(135deg, var(--vs-primary), var(--vs-primary-2)); color: #fff; font-weight: 800; display: grid; place-items: center; }
.vs-step-icon { font-size: 1.6rem; color: var(--vs-primary); margin: .6rem 0 .8rem; }
.vs-step-title { font-size: 1.15rem; margin-bottom: .4rem; }
.vs-step-body { color: var(--vs-muted); margin: 0; }

/* ---------- Confidence section ---------- */
.vs-confidence { background: linear-gradient(135deg, var(--vs-primary-d), var(--vs-primary)); color: #fff; border-radius: 24px; padding: 3rem; box-shadow: var(--vs-shadow); }
.vs-confidence h2 { color: #fff; }
.vs-confidence .vs-lead { color: rgba(255,255,255,.85); }
.vs-confidence-list { list-style: none; padding: 0; margin: 1.5rem 0 0; display: grid; grid-template-columns: 1fr 1fr; gap: .9rem; }
.vs-confidence-list li { display: flex; align-items: flex-start; gap: .6rem; font-weight: 600; }
.vs-confidence-list i { margin-top: .15rem; }

/* ---------- FAQ ---------- */
.vs-faq { max-width: 800px; margin: 0 auto; }
.vs-faq-item { background: #fff; border: 1px solid var(--vs-line); border-radius: 12px; padding: 1rem 1.3rem; margin-bottom: .8rem; box-shadow: var(--vs-shadow-sm); }
.vs-faq-q { font-weight: 700; color: var(--vs-ink); cursor: pointer; list-style: none; display: flex; justify-content: space-between; align-items: center; }
.vs-faq-q::after { content: "+"; color: var(--vs-primary); font-weight: 800; font-size: 1.3rem; }
.vs-faq-item[open] .vs-faq-q::after { content: "\2212"; }
.vs-faq-q::-webkit-details-marker { display: none; }
.vs-faq-a { color: var(--vs-muted); padding-top: .8rem; }

/* ---------- Final CTA ---------- */
.vs-finalcta { text-align: center; padding: 4.5rem 1rem; background: var(--vs-bg-alt); }

/* ---------- Footer ---------- */
.vs-footer { padding: 2.5rem 1rem; border-top: 1px solid var(--vs-line); color: var(--vs-muted); }
.vs-footer-links { display: flex; flex-wrap: wrap; gap: 1.4rem; justify-content: center; margin-top: .8rem; }
.vs-footer-links a { color: var(--vs-body); font-weight: 600; }
.vs-footer-links a:hover { color: var(--vs-primary); }

/* ---------- Responsive ---------- */
@media (max-width: 767px) {
    .vs-confidence-list { grid-template-columns: 1fr; }
    .vs-confidence { padding: 2rem; }
    .vs-topnav a:not(.btn) { display: none; }
}
```

- [ ] **Step 2: Link the stylesheet in App.razor**

In `src/VanillaStudio/Components/App.razor`, add the landing stylesheet link immediately after the existing `wizard.css` link (line 11):

```razor
    <link rel="stylesheet" href="@Assets["css/wizard.css"]" />
    <link rel="stylesheet" href="@Assets["css/landing.css"]" />
```

- [ ] **Step 3: Build to verify the project still compiles**

Run: `dotnet build src/VanillaStudio/ZKnow.VanillaStudio.csproj`
Expected: `Build succeeded` with 0 errors. (If the csproj filename differs, use the actual `.csproj` in `src/VanillaStudio/`.)

- [ ] **Step 4: Commit**

```bash
git add src/VanillaStudio/wwwroot/css/landing.css src/VanillaStudio/Components/App.razor
git commit -m "feat(landing): add clean .NET-modern theme stylesheet"
```

---

## Task 2: SEO crawler assets

**Files:**
- Create: `src/VanillaStudio/wwwroot/robots.txt`
- Create: `src/VanillaStudio/wwwroot/sitemap.xml`
- Create: `src/VanillaStudio/wwwroot/og-image.png` (placeholder copied from favicon)

**Interfaces:**
- Consumes: nothing.
- Produces: `/og-image.png`, `/robots.txt`, `/sitemap.xml` served as static files; `og-image.png` is referenced by Task 5's OG/Twitter tags.

- [ ] **Step 1: Create robots.txt**

Create `src/VanillaStudio/wwwroot/robots.txt`:

```text
User-agent: *
Allow: /

Sitemap: https://vanillaslice.dev/sitemap.xml
```

- [ ] **Step 2: Create sitemap.xml**

Create `src/VanillaStudio/wwwroot/sitemap.xml`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>https://vanillaslice.dev/</loc>
    <changefreq>weekly</changefreq>
    <priority>1.0</priority>
  </url>
  <url>
    <loc>https://vanillaslice.dev/wizard</loc>
    <changefreq>monthly</changefreq>
    <priority>0.8</priority>
  </url>
</urlset>
```

- [ ] **Step 3: Create a placeholder OG image**

Copy the existing favicon to a placeholder OG image (valid PNG, swap with a real 1200×630 asset later).

Run: `Copy-Item src/VanillaStudio/wwwroot/favicon.png src/VanillaStudio/wwwroot/og-image.png`
Expected: `og-image.png` now exists in `wwwroot`.

- [ ] **Step 4: Commit**

```bash
git add src/VanillaStudio/wwwroot/robots.txt src/VanillaStudio/wwwroot/sitemap.xml src/VanillaStudio/wwwroot/og-image.png
git commit -m "feat(landing): add SEO crawler assets (robots, sitemap, og placeholder)"
```

---

## Task 3: Reusable presentational components

**Files:**
- Create: `src/VanillaStudio/Components/Pages/Landing/FeatureCard.razor`
- Create: `src/VanillaStudio/Components/Pages/Landing/StepCard.razor`
- Create: `src/VanillaStudio/Components/Pages/Landing/FaqItem.razor`

**Interfaces:**
- Consumes: CSS classes from Task 1.
- Produces (used by Task 5):
  - `<FeatureCard Icon="string" Title="string" Body="string" Status="string?" StatusClass="string?" />`
  - `<StepCard Number="int" Icon="string" Title="string" Body="string" />`
  - `<FaqItem Question="string" Answer="string" />`

- [ ] **Step 1: Create FeatureCard.razor**

Create `src/VanillaStudio/Components/Pages/Landing/FeatureCard.razor`:

```razor
<div class="col-md-4 mb-4">
    <div class="vs-feature-card h-100">
        <div class="vs-feature-icon"><i class="bi @Icon"></i></div>
        <div class="vs-feature-head">
            <h3 class="vs-feature-title">@Title</h3>
            @if (!string.IsNullOrEmpty(Status))
            {
                <span class="vs-badge @StatusClass">@Status</span>
            }
        </div>
        <p class="vs-feature-body">@Body</p>
    </div>
</div>

@code {
    [Parameter, EditorRequired] public string Icon { get; set; } = "";
    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter, EditorRequired] public string Body { get; set; } = "";
    [Parameter] public string? Status { get; set; }
    [Parameter] public string? StatusClass { get; set; }
}
```

- [ ] **Step 2: Create StepCard.razor**

Create `src/VanillaStudio/Components/Pages/Landing/StepCard.razor`:

```razor
<div class="col-md-4 mb-4">
    <div class="vs-step-card">
        <div class="vs-step-number">@Number</div>
        <div class="vs-step-icon"><i class="bi @Icon"></i></div>
        <h3 class="vs-step-title">@Title</h3>
        <p class="vs-step-body">@Body</p>
    </div>
</div>

@code {
    [Parameter, EditorRequired] public int Number { get; set; }
    [Parameter, EditorRequired] public string Icon { get; set; } = "";
    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter, EditorRequired] public string Body { get; set; } = "";
}
```

- [ ] **Step 3: Create FaqItem.razor**

Native `<details>`/`<summary>` gives a collapsible FAQ with **no JavaScript** — essential for a static-SSR page.

Create `src/VanillaStudio/Components/Pages/Landing/FaqItem.razor`:

```razor
<details class="vs-faq-item">
    <summary class="vs-faq-q">@Question</summary>
    <div class="vs-faq-a">@Answer</div>
</details>

@code {
    [Parameter, EditorRequired] public string Question { get; set; } = "";
    [Parameter, EditorRequired] public string Answer { get; set; } = "";
}
```

- [ ] **Step 4: Build to verify the components compile**

Run: `dotnet build src/VanillaStudio/ZKnow.VanillaStudio.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/VanillaStudio/Components/Pages/Landing/FeatureCard.razor src/VanillaStudio/Components/Pages/Landing/StepCard.razor src/VanillaStudio/Components/Pages/Landing/FaqItem.razor
git commit -m "feat(landing): add reusable feature/step/faq components"
```

---

## Task 4: Hero section component

**Files:**
- Create: `src/VanillaStudio/Components/Pages/Landing/HeroSection.razor`

**Interfaces:**
- Consumes: CSS classes from Task 1; the constants `GitHubUrl` is inlined here as a literal (the page-level constant lives in Task 5; the hero uses the literal URL directly to stay self-contained).
- Produces: `<HeroSection />` (no parameters) used by Task 5.

- [ ] **Step 1: Create HeroSection.razor**

Create `src/VanillaStudio/Components/Pages/Landing/HeroSection.razor`:

```razor
<section class="vs-hero">
    <div class="container">
        <div class="row align-items-center g-5">
            <div class="col-lg-6">
                <span class="vs-eyebrow">Open Source · MIT · .NET 9</span>
                <h1 class="vs-hero-title">Vertical Slice + Clean Architecture for .NET — free, open, and yours to keep.</h1>
                <p class="vs-hero-sub">
                    VanillaSlice generates complete, end-to-end feature slices for Blazor Web and .NET MAUI.
                    It is code that generates code — every line is plain C# you own, tweak, and ship.
                    No runtime dependency, no lock-in, nothing to monetize.
                </p>
                <div class="vs-hero-cta">
                    <a class="btn vs-btn-primary btn-lg" href="/wizard">
                        <i class="bi bi-magic"></i> Generate Your Project
                    </a>
                    <a class="btn vs-btn-ghost btn-lg" href="https://github.com/zero-know/VanillaSlice" target="_blank" rel="noopener">
                        <i class="bi bi-github"></i> View Source on GitHub
                    </a>
                </div>
            </div>
            <div class="col-lg-6">
                <div class="vs-code-window" aria-hidden="true">
                    <div class="vs-code-bar"><span></span><span></span><span></span></div>
<pre class="vs-code-body"><code>Products/                         # one self-contained slice
├─ ProductListing/
│  ├─ ProductListing.razor        # UI
│  ├─ ProductListingViewModel.cs
│  ├─ ProductListingClient.cs     # typed HttpClient
│  ├─ ProductListingController.cs # REST endpoint
│  └─ ProductListingService.cs    # business logic + queries
└─ ProductForm/
   ├─ ProductForm.razor
   ├─ ProductFormViewModel.cs
   ├─ ProductFormClient.cs
   ├─ ProductFormController.cs
   └─ ProductFormService.cs

# DI, routing, endpoints — all pre-wired.
# Add fields, implement queries. Done.</code></pre>
                </div>
            </div>
        </div>
    </div>
</section>
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/VanillaStudio/ZKnow.VanillaStudio.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/VanillaStudio/Components/Pages/Landing/HeroSection.razor
git commit -m "feat(landing): add hero section with code-window visual"
```

---

## Task 5: Landing page with full SEO head

**Files:**
- Rewrite: `src/VanillaStudio/Components/Pages/Home.razor`

**Interfaces:**
- Consumes: `<HeroSection />`, `<FeatureCard />`, `<StepCard />`, `<FaqItem />` from Tasks 3–4; CSS from Task 1; `/og-image.png` from Task 2.
- Produces: the public `/` route. Terminal deliverable.

- [ ] **Step 1: Rewrite Home.razor**

Replace the entire contents of `src/VanillaStudio/Components/Pages/Home.razor` with:

```razor
@page "/"
@using System.Text.Json
@using ZKnow.VanillaStudio.Components.Pages.Landing

<PageTitle>VanillaSlice — Free Vertical Slice & Clean Architecture Templates for .NET (Blazor & MAUI)</PageTitle>

<HeadContent>
    <meta name="description" content="VanillaSlice is a free, open-source code generator for .NET that scaffolds vertical-slice, clean-architecture features for Blazor Web and MAUI. Full source, MIT licensed, no lock-in." />
    <meta name="keywords" content="vertical slice architecture, clean architecture, .NET templates, Blazor templates, MAUI templates, free code generator, SOLID, open source, C#, CRUD scaffolding" />
    <meta name="robots" content="index, follow" />
    <meta name="author" content="VanillaSlice" />
    <link rel="canonical" href="https://vanillaslice.dev/" />

    <!-- Open Graph -->
    <meta property="og:type" content="website" />
    <meta property="og:site_name" content="VanillaSlice" />
    <meta property="og:title" content="VanillaSlice — Free Vertical Slice & Clean Architecture Templates for .NET" />
    <meta property="og:description" content="A free, open-source code generator for .NET. Scaffold vertical-slice, clean-architecture features for Blazor Web and MAUI. Full source, MIT, no lock-in." />
    <meta property="og:url" content="https://vanillaslice.dev/" />
    <meta property="og:image" content="https://vanillaslice.dev/og-image.png" />

    <!-- Twitter -->
    <meta name="twitter:card" content="summary_large_image" />
    <meta name="twitter:title" content="VanillaSlice — Free Vertical Slice & Clean Architecture Templates for .NET" />
    <meta name="twitter:description" content="A free, open-source code generator for .NET. Scaffold vertical-slice, clean-architecture features for Blazor Web and MAUI. Full source, MIT, no lock-in." />
    <meta name="twitter:image" content="https://vanillaslice.dev/og-image.png" />

    <script type="application/ld+json">@((MarkupString)SoftwareAppJsonLd)</script>
    <script type="application/ld+json">@((MarkupString)FaqJsonLd)</script>
</HeadContent>

<div class="vs-page">

    <!-- Top bar -->
    <header class="vs-topbar">
        <a class="vs-wordmark" href="/"><i class="bi bi-layers-half"></i> VanillaSlice</a>
        <nav class="vs-topnav">
            <a href="#architecture">Architecture</a>
            <a href="#features">Features</a>
            <a href="#faq">FAQ</a>
            <a href="@GitHubUrl" target="_blank" rel="noopener">GitHub</a>
            <a class="btn vs-btn-primary" href="/wizard">Generate Project</a>
        </nav>
    </header>

    <HeroSection />

    <!-- Trust bar -->
    <div class="vs-trustbar">
        <span class="vs-chip"><i class="bi bi-cash-coin"></i> 100% Free</span>
        <span class="vs-chip"><i class="bi bi-shield-check"></i> MIT Licensed</span>
        <span class="vs-chip"><i class="bi bi-file-earmark-code"></i> Full Source Included</span>
        <span class="vs-chip"><i class="bi bi-plug"></i> No Runtime Dependency</span>
        <span class="vs-chip"><i class="bi bi-lock"></i> Nothing to Monetize, Ever</span>
    </div>

    <!-- Architecture -->
    <section id="architecture" class="vs-section">
        <div class="container">
            <div class="vs-section-head">
                <div class="vs-kicker">Why VanillaSlice</div>
                <h2 class="vs-h2">Clean Architecture, organized as Vertical Slices</h2>
                <p class="vs-lead">
                    Instead of spreading one feature across controller, service, and UI folders, each slice owns its
                    whole vertical: UI → contract → domain logic → data. The result enforces SOLID by design — features
                    stay independent, easy to test, and safe to change.
                </p>
            </div>
            <div class="row">
                <FeatureCard Icon="bi-diagram-3-fill" Title="Self-contained slices"
                             Body="Every feature ships with its Razor/XAML UI, ViewModel, typed client, controller, and server service — owned end-to-end." />
                <FeatureCard Icon="bi-shield-lock-fill" Title="SOLID by default"
                             Body="The factory enforces structure through interfaces and safe stubs, so you write domain logic — not plumbing." />
                <FeatureCard Icon="bi-arrow-repeat" Title="Absorbs .NET evolution"
                             Body="Thin, feature-centric slices keep regressions low and let your codebase grow with the platform and AI co-authoring." />
            </div>
        </div>
    </section>

    <!-- How it works -->
    <section class="vs-section vs-section-alt">
        <div class="container">
            <div class="vs-section-head">
                <div class="vs-kicker">How it works</div>
                <h2 class="vs-h2">From idea to a runnable solution in three steps</h2>
            </div>
            <div class="row">
                <StepCard Number="1" Icon="bi-sliders" Title="Configure in the wizard"
                          Body="Pick your platform (Blazor Web or Web + MAUI), UI framework, database, and auth. No setup, no install." />
                <StepCard Number="2" Icon="bi-magic" Title="Generate the solution"
                          Body="Download a complete, pre-wired solution — projects, DI, routing, endpoints, and sample CRUD slices included." />
                <StepCard Number="3" Icon="bi-pencil-square" Title="Own & tweak every line"
                          Body="It's plain C#. Add fields, implement queries, and ship. There's no SDK between you and your code." />
            </div>
        </div>
    </section>

    <!-- Features -->
    <section id="features" class="vs-section">
        <div class="container">
            <div class="vs-section-head">
                <div class="vs-kicker">What you get</div>
                <h2 class="vs-h2">Batteries included, your choice of stack</h2>
            </div>
            <div class="row">
                <FeatureCard Icon="bi-window-stack" Title="Blazor Web" Status="Ready" StatusClass="vs-badge-done"
                             Body="Server-side and WebAssembly rendering with configurable render modes." />
                <FeatureCard Icon="bi-phone" Title="MAUI Hybrid" Status="Ready" StatusClass="vs-badge-done"
                             Body="Cross-platform mobile from the same shared Razor components." />
                <FeatureCard Icon="bi-phone-vibrate" Title="MAUI Native" Status="Ready" StatusClass="vs-badge-done"
                             Body="Native XAML frontend over the same shared backend slices." />
                <FeatureCard Icon="bi-palette" Title="UI Frameworks" Status="Ready" StatusClass="vs-badge-done"
                             Body="Bootstrap 5, Microsoft Fluent UI, and Tailwind CSS implemented out of the box." />
                <FeatureCard Icon="bi-database-fill" Title="Database & EF Core" Status="Ready" StatusClass="vs-badge-done"
                             Body="SQL Server with Entity Framework Core — migrations, seeding, and the repository pattern." />
                <FeatureCard Icon="bi-shield-lock" Title="Auth & Security" Status="Ready" StatusClass="vs-badge-done"
                             Body="ASP.NET Core Identity, authorization policies, and JWT token support." />
            </div>
        </div>
    </section>

    <!-- Confidence -->
    <section class="vs-section">
        <div class="container">
            <div class="vs-confidence">
                <div class="vs-section-head" style="margin-bottom:1rem;">
                    <h2 class="vs-h2">It's code that generates code</h2>
                    <p class="vs-lead">VanillaSlice writes a starting point and then gets out of your way. There is nothing to lock you in — and nothing we could ever charge you for.</p>
                </div>
                <ul class="vs-confidence-list">
                    <li><i class="bi bi-check-circle-fill"></i> No runtime SDK, package, or service dependency — the generated code is fully standalone.</li>
                    <li><i class="bi bi-check-circle-fill"></i> MIT licensed, full source on GitHub — fork it, audit it, change anything.</li>
                    <li><i class="bi bi-check-circle-fill"></i> Use the generated code commercially with zero obligations.</li>
                    <li><i class="bi bi-check-circle-fill"></i> No accounts, no telemetry paywall, no "pro tier" — and no plan to add one.</li>
                </ul>
            </div>
        </div>
    </section>

    <!-- FAQ -->
    <section id="faq" class="vs-section vs-section-alt">
        <div class="container">
            <div class="vs-section-head">
                <div class="vs-kicker">FAQ</div>
                <h2 class="vs-h2">Questions, answered</h2>
            </div>
            <div class="vs-faq">
                @foreach (var f in Faqs)
                {
                    <FaqItem Question="@f.Question" Answer="@f.Answer" />
                }
            </div>
        </div>
    </section>

    <!-- Final CTA -->
    <section class="vs-finalcta">
        <div class="container">
            <h2 class="vs-h2">Generate your project now</h2>
            <p class="vs-lead mb-4">Free, in your browser, no sign-up. You'll have a runnable solution in minutes.</p>
            <a class="btn vs-btn-primary btn-lg" href="/wizard"><i class="bi bi-magic"></i> Start the Wizard</a>
        </div>
    </section>

    <!-- Footer -->
    <footer class="vs-footer">
        <div class="container text-center">
            <div class="vs-wordmark justify-content-center"><i class="bi bi-layers-half"></i> VanillaSlice</div>
            <div class="vs-footer-links">
                <a href="@GitHubUrl" target="_blank" rel="noopener">GitHub</a>
                <a href="@($"{GitHubUrl}/releases")" target="_blank" rel="noopener">Releases</a>
                <a href="@($"{GitHubUrl}/blob/main/LICENSE")" target="_blank" rel="noopener">MIT License</a>
                <a href="/wizard">Generate Project</a>
            </div>
            <p class="small mt-3 mb-0">Free &amp; open-source under the MIT License.</p>
        </div>
    </footer>

</div>

@code {
    private const string GitHubUrl = "https://github.com/zero-know/VanillaSlice";

    private record Faq(string Question, string Answer);

    private static readonly Faq[] Faqs = new[]
    {
        new Faq("Is VanillaSlice really free?",
            "Yes. It is open-source under the MIT License, with the full source available on GitHub. There is no paid tier, no account, and no usage limit."),
        new Faq("What is vertical slice architecture?",
            "Vertical slice architecture organizes code by feature rather than by technical layer. Each slice contains everything one feature needs — UI, contracts, domain logic, and data access — so features stay independent and easy to change."),
        new Faq("Do I depend on VanillaSlice at runtime?",
            "No. VanillaSlice generates plain C# source code into your solution. Once generated, your project has no dependency on VanillaSlice — it runs entirely on standard .NET."),
        new Faq("Can I use the generated code commercially?",
            "Absolutely. The MIT License lets you use, modify, and distribute the generated code in commercial products with no obligations."),
        new Faq("Will VanillaSlice ever be monetized?",
            "No. The generated code is standalone and there is no runtime service to charge for. The project is open-source by design, with nothing to lock you in.")
    };

    private static readonly JsonSerializerOptions JsonSeoOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private const string SoftwareAppJsonLd = """
    {
      "@context": "https://schema.org",
      "@type": "SoftwareApplication",
      "name": "VanillaSlice",
      "description": "A free, open-source code generator for .NET that scaffolds vertical-slice, clean-architecture features for Blazor Web and MAUI.",
      "applicationCategory": "DeveloperApplication",
      "operatingSystem": "Windows, macOS, Linux",
      "url": "https://vanillaslice.dev/",
      "license": "https://opensource.org/licenses/MIT",
      "sameAs": "https://github.com/zero-know/VanillaSlice",
      "offers": { "@type": "Offer", "price": "0", "priceCurrency": "USD" }
    }
    """;

    private string FaqJsonLd => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "FAQPage",
        ["mainEntity"] = Faqs.Select(f => new Dictionary<string, object?>
        {
            ["@type"] = "Question",
            ["name"] = f.Question,
            ["acceptedAnswer"] = new Dictionary<string, object?>
            {
                ["@type"] = "Answer",
                ["text"] = f.Answer
            }
        }).ToArray()
    }, JsonSeoOptions);
}
```

**Note on the `@using ZKnow.VanillaStudio.Components.Pages.Landing` line:** confirm the root namespace by checking `_Imports.razor` or the `.csproj` `RootNamespace`. If the assembly root namespace is not `ZKnow.VanillaStudio`, adjust the `@using` to the actual `<RootNamespace>.Components.Pages.Landing`. If the Landing components are already discoverable via an existing `@using` in `_Imports.razor`, this line can be omitted.

- [ ] **Step 2: Build**

Run: `dotnet build src/VanillaStudio/ZKnow.VanillaStudio.csproj`
Expected: `Build succeeded` with 0 errors. If you get "type FeatureCard not found", fix the `@using` namespace per the note above.

- [ ] **Step 3: Run the app and verify the SSR HTML contains the SEO payload**

Start the app in the background:

Run: `dotnet run --project src/VanillaStudio/ZKnow.VanillaStudio.csproj`

Then fetch the rendered root and assert the key SEO + CTA strings are present in the **raw server HTML** (this proves static SSR delivery, not client rendering). Use the actual URL from the app's console output (commonly `http://localhost:5000` or `https://localhost:5001`):

Run (PowerShell):
```powershell
$html = (Invoke-WebRequest -Uri "http://localhost:5000/" -UseBasicParsing).Content
$checks = @('og:title','application/ld+json','FAQPage','SoftwareApplication','/wizard','Vertical Slice','canonical')
$checks | ForEach-Object { "{0,-22} {1}" -f $_, ($html -match [regex]::Escape($_)) }
```
Expected: every line prints `True`.

- [ ] **Step 4: Visual + responsive spot check**

Open the running app's URL in a browser. Confirm:
- Hero renders with the purple gradient, headline, both CTA buttons, and the code window.
- Trust chips, feature cards, step cards, confidence panel, and FAQ all render.
- Clicking **Generate Your Project** / **Start the Wizard** navigates to `/wizard`.
- FAQ items expand/collapse on click (native `<details>`, no JS).
- At a narrow (~375px) width the layout stacks cleanly.

Stop the background app once verified.

- [ ] **Step 5: Commit**

```bash
git add src/VanillaStudio/Components/Pages/Home.razor
git commit -m "feat(landing): rewrite / as SEO-rich marketing landing page"
```

---

## Self-Review (completed during planning)

- **Spec coverage:** Route replacement (Task 5) ✓; primary CTA → `/wizard` (Hero, top bar, steps, final CTA) ✓; SEO meta/keywords/canonical/OG/Twitter/JSON-LD (Task 5 head) ✓; `robots.txt`/`sitemap.xml` (Task 2) ✓; clean .NET-modern theme (Task 1) ✓; CSS code visual + placeholder OG (Task 4 / Task 2) ✓; canonical domain & GitHub URL constants ✓; trust/confidence + FAQ messaging (Task 5) ✓; honest feature status badges (Task 5) ✓; testing via build + view-source (Task 5) ✓.
- **Placeholder scan:** No TBD/TODO; the only "placeholder" is the intentional `og-image.png` (documented in spec), created as a valid PNG.
- **Type consistency:** Component parameter names (`Icon`, `Title`, `Body`, `Status`, `StatusClass`, `Number`, `Question`, `Answer`) match between definitions (Tasks 3–4) and usages (Task 5). JSON-LD uses `Dictionary<string, object?>` to emit `@context`/`@type` keys correctly (anonymous objects can't). `Faqs` data feeds both the rendered `<FaqItem>`s and the `FaqJsonLd` — single source of truth (DRY).
```
