# Postnomic Opt-In Token Theming (Both Packages) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an opt-in `Semantic` markup mode to both `Postnomic.Client.AspNetCore` and `Postnomic.Client.Blazor` so consumers can theme the blog with CSS variables, while the default `Bootstrap` mode keeps every existing consumer byte-unchanged. Ship a shared variable-driven stylesheet, make the sitemap/RSS builders reusable from a Blazor host, and release v1.3.0.

**Architecture:** A shared, framework-free `PostnomicCssClasses` resolver in `Abstractions` maps each semantic role (Card, PostTitle, Btn…) to a class string, keyed by a new `PostnomicMarkupStyle { Bootstrap, Semantic }` option (default `Bootstrap`). Both the Blazor components (`.razor`) and the AspNetCore Blog Area (`.cshtml`) replace hard-coded class literals with resolver lookups. A single `postnomic-blog.css` (variable-driven) is linked into both packages' `wwwroot`. Default output is identical to today; Semantic emits `pn-*`.

**Tech Stack:** .NET 10, `Microsoft.NET.Sdk.Razor` (both packages), bunit, `Microsoft.AspNetCore.Mvc.Testing`, xUnit, FluentAssertions, MinVer.

## Global Constraints

- Repo: `postnomic-dotnet` (OutaStory's submodule `lib/postnomic-dotnet`). Commit, tag, release here; then bump the parent `threeb-it/Postnomic` submodule pointer.
- Test convention THIS repo: **xUnit + FluentAssertions 8.10.0 + Moq + bunit 2.7.2** (do NOT import OutaStory's no-FluentAssertions rule).
- Coverage gate: **≥80% line per project**, CI-enforced.
- Versioning: MinVer from `v*` tags; target **v1.3.0** (additive/minor).
- **Default `MarkupStyle` is `Bootstrap`.** Default-mode rendered markup of BOTH packages must be byte-identical to pre-change (Xircuit regression guard). Any diff in default mode is a bug.
- Both packages are `Microsoft.NET.Sdk.Razor`; `wwwroot` files ship as `_content/<PackageId>/…` static web assets automatically.
- NuGet.org publish requires operator approval of the `nuget-publish` GitHub Environment gate — an agent cannot approve it.

---

### Task 1: Shared markup-style switch + class resolver (`Abstractions`)

**Files:**
- Create: `src/Postnomic.Client.Abstractions/PostnomicMarkupStyle.cs`
- Create: `src/Postnomic.Client.Abstractions/PostnomicCssClasses.cs`
- Modify: `src/Postnomic.Client.Abstractions/PostnomicClientOptions.cs` (add `MarkupStyle`)
- Test: `tests/Postnomic.Client.Abstractions.Tests/PostnomicCssClassesTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace Postnomic.Client.Abstractions;
  public enum PostnomicMarkupStyle { Bootstrap = 0, Semantic = 1 }

  // On PostnomicClientOptions:
  public PostnomicMarkupStyle MarkupStyle { get; set; } = PostnomicMarkupStyle.Bootstrap;

  public sealed class PostnomicCssClasses
  {
      public PostnomicCssClasses(PostnomicMarkupStyle style);
      public string BlogRoot { get; } public string Header { get; } public string Title { get; }
      public string Lead { get; } public string Layout { get; } public string Main { get; }
      public string Sidebar { get; } public string Card { get; } public string CardMedia { get; }
      public string CardBody { get; } public string PostTitle { get; } public string PostMeta { get; }
      public string Excerpt { get; } public string Tag { get; } public string TagCategory { get; }
      public string BtnPrimary { get; } public string BtnOutline { get; } public string BtnSm { get; }
      public string Pagination { get; } public string Page { get; } public string PageActive { get; }
      public string PageDisabled { get; } public string FilterBanner { get; } public string Empty { get; }
      public string Loading { get; } public string Masonry { get; } public string Widget { get; }
      public string WidgetTitle { get; } public string SearchBox { get; } public string Comment { get; }
      public string CommentForm { get; } public string Field { get; } public string PostContent { get; }
  }
  ```
- Consumed by: Tasks 3 (Blazor) & 4 (AspNetCore), and OutaStory Plan (DI sets `MarkupStyle = Semantic`).

**Class table (Bootstrap value = the literal currently in the markup — verify against the files; Semantic = `pn-*`):**

| Role | Bootstrap | Semantic |
|---|---|---|
| BlogRoot | `blog-container` | `pn-blog` |
| Header | `blog-header text-center py-4 mb-4 border-bottom` | `pn-header` |
| Title | `display-5 fw-bold` | `pn-title` |
| Lead | `lead text-muted` | `pn-lead` |
| Layout | `row` | `pn-layout` |
| Main | `col-lg-8` | `pn-main` |
| Sidebar | `col-lg-4` | `pn-sidebar` |
| Card | `card mb-4 shadow-sm` | `pn-card` |
| CardMedia | `card-img-top` | `pn-card__media` |
| CardBody | `card-body` | `pn-card__body` |
| PostTitle | `card-title h4` | `pn-post-title` |
| PostMeta | `text-muted small mb-2` | `pn-post-meta` |
| Excerpt | `card-text` | `pn-excerpt` |
| Tag | `badge bg-secondary me-1` | `pn-tag` |
| TagCategory | `badge bg-primary me-1` | `pn-tag pn-tag--category` |
| BtnPrimary | `btn btn-primary` | `pn-btn pn-btn--primary` |
| BtnOutline | `btn btn-outline-primary btn-sm` | `pn-btn pn-btn--outline pn-btn--sm` |
| BtnSm | `btn btn-sm btn-outline-secondary` | `pn-btn pn-btn--sm pn-btn--outline` |
| Pagination | `pagination` | `pn-pagination` |
| Page | `page-item` | `pn-page` |
| PageActive | `active` | `pn-page--active` |
| PageDisabled | `disabled` | `pn-page--disabled` |
| FilterBanner | `alert alert-info d-flex justify-content-between align-items-center mb-3` | `pn-filter-banner` |
| Empty | `text-center text-muted py-5` | `pn-empty` |
| Loading | `text-center py-5` | `pn-loading` |
| Masonry | `postnomic-masonry` | `pn-masonry` |
| Widget | `card mb-3` | `pn-widget` |
| WidgetTitle | `card-header` | `pn-widget__title` |
| SearchBox | `input-group mb-3` | `pn-searchbox` |
| Comment | `border-bottom pb-2 mb-2` | `pn-comment` |
| CommentForm | `mb-3` | `pn-comment-form` |
| Field | `form-control` | `pn-field` |
| PostContent | `postnomic-post-content` | `pn-post-content` |

(When applying Tasks 3–4, read each file to confirm the exact current literal for any row and adjust the Bootstrap column so default output is truly unchanged.)

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Postnomic.Client.Abstractions;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

public class PostnomicCssClassesTests
{
    [Fact]
    public void Bootstrap_mode_returns_legacy_classes()
    {
        var c = new PostnomicCssClasses(PostnomicMarkupStyle.Bootstrap);
        c.Card.Should().Be("card mb-4 shadow-sm");
        c.BtnOutline.Should().Be("btn btn-outline-primary btn-sm");
        c.Layout.Should().Be("row");
    }

    [Fact]
    public void Semantic_mode_returns_pn_classes()
    {
        var c = new PostnomicCssClasses(PostnomicMarkupStyle.Semantic);
        c.Card.Should().Be("pn-card");
        c.BtnOutline.Should().Be("pn-btn pn-btn--outline pn-btn--sm");
        c.Layout.Should().Be("pn-layout");
    }

    [Fact]
    public void Options_default_markup_style_is_bootstrap() =>
        new PostnomicClientOptions { BaseUrl = "x", ApiKey = "k", BlogSlug = "b" }
            .MarkupStyle.Should().Be(PostnomicMarkupStyle.Bootstrap);
}
```

- [ ] **Step 2: Run — expect FAIL** (types missing)

Run: `dotnet test tests/Postnomic.Client.Abstractions.Tests --filter PostnomicCssClassesTests`
Expected: FAIL (type/member not found).

- [ ] **Step 3: Implement** the enum, the `MarkupStyle` option (default `Bootstrap`), and `PostnomicCssClasses` (a `switch` on style per role, values from the table).

- [ ] **Step 4: Run — expect PASS**

Run: `dotnet test tests/Postnomic.Client.Abstractions.Tests --filter PostnomicCssClassesTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Postnomic.Client.Abstractions tests/Postnomic.Client.Abstractions.Tests/PostnomicCssClassesTests.cs
git commit -m "feat(abstractions): opt-in MarkupStyle + PostnomicCssClasses resolver"
```

---

### Task 2: Shared variable-driven stylesheet (both packages)

**Files:**
- Create: `assets/postnomic-blog.css` (single source at repo root `assets/`)
- Modify: `src/Postnomic.Client.Blazor/Postnomic.Client.Blazor.csproj` and `src/Postnomic.Client.AspNetCore/Postnomic.Client.AspNetCore.csproj` — link the shared CSS into each `wwwroot`:
  ```xml
  <ItemGroup>
    <Content Include="..\..\assets\postnomic-blog.css" Link="wwwroot\postnomic-blog.css" />
  </ItemGroup>
  ```
- Test: `tests/Postnomic.Client.Abstractions.Tests/DefaultStylesheetTests.cs`

**Interfaces:**
- Produces: `_content/Postnomic.Client.Blazor/postnomic-blog.css` and `_content/Postnomic.Client.AspNetCore/postnomic-blog.css`; the `--pn-*` variable + `pn-*` class contract.

**`--pn-*` variables (defaults) + classes:** as listed in the spec Part A.3. Style every`pn-*` class from`--pn-*` only. 2-col`.pn-layout` grid (`grid-template-columns: minmax(0,1fr) 320px; gap: var(--pn-space-lg)`; ≤768px →`1fr`), responsive rules prefixed`.pn-layout > `.`.pn-post-content` long-form typography.`.pn-masonry` columns collapse to 1 at ≤768px.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

public class DefaultStylesheetTests
{
    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Postnomic.Client.slnx"))) d = d.Parent;
        d.Should().NotBeNull(); return d!.FullName;
    }

    [Fact]
    public void Shared_stylesheet_exists_and_defines_contract()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "assets", "postnomic-blog.css"));
        foreach (var token in new[] { "--pn-surface", "--pn-primary", "--pn-radius", ".pn-blog", ".pn-card", ".pn-btn", ".pn-post-content" })
            css.Should().Contain(token);
        css.Should().NotContain(".col-lg");
    }

    [Theory]
    [InlineData("src/Postnomic.Client.Blazor/Postnomic.Client.Blazor.csproj")]
    [InlineData("src/Postnomic.Client.AspNetCore/Postnomic.Client.AspNetCore.csproj")]
    public void Both_packages_ship_the_stylesheet(string csprojRelative)
    {
        var proj = File.ReadAllText(Path.Combine(RepoRoot(), csprojRelative));
        proj.Should().Contain("postnomic-blog.css");
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test tests/Postnomic.Client.Abstractions.Tests --filter DefaultStylesheetTests`
Expected: FAIL.

- [ ] **Step 3: Author `assets/postnomic-blog.css`** (full variable-driven stylesheet) and add the `<Content Include… Link="wwwroot\postnomic-blog.css" />` item to both csprojs.

- [ ] **Step 4: Run — expect PASS**

Run: `dotnet test tests/Postnomic.Client.Abstractions.Tests --filter DefaultStylesheetTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add assets/postnomic-blog.css src/Postnomic.Client.Blazor/*.csproj src/Postnomic.Client.AspNetCore/*.csproj tests/Postnomic.Client.Abstractions.Tests/DefaultStylesheetTests.cs
git commit -m "feat: ship shared variable-driven blog stylesheet in both packages"
```

---

### Task 3: Blazor components honor `MarkupStyle`

**Files:**
- Modify: `src/Postnomic.Client.Blazor/Components/Pages/{BlogPage,PostPage,AuthorPage}.razor`, `Components/CommentView.razor`, `Components/Sidebar/*.razor`
- Test: `tests/Postnomic.Client.Blazor.Tests/MarkupStyleTests.cs`

**Interfaces:**
- Consumes: `PostnomicCssClasses`, `PostnomicMarkupStyle`, `PostnomicClientOptions.MarkupStyle` (Task 1).

**Pattern:** in each component's `@code`, add
```csharp
private PostnomicMarkupStyle MarkupStyle =>
    BlogContext?.Options.MarkupStyle ?? _injectedClientOptions.Value.MarkupStyle;
private PostnomicCssClasses Cls => new(MarkupStyle);
```
Replace each hard-coded class literal with `@Cls.Card`, `@Cls.PostTitle`, etc. Where an
`<i class="bi bi-*">` icon appears: keep it as-is in Bootstrap mode, render an inline SVG
in Semantic mode (`@if (MarkupStyle == PostnomicMarkupStyle.Semantic) { <svg…/> } else { <i class="bi bi-…"></i> }`).
Move the inline `<style>` blocks (masonry/img) into `assets/postnomic-blog.css` under
`pn-*` scope; keep an equivalent inline rule only for Bootstrap mode if a component
relied on it. Sidebar widgets map `card mb-3`/`card-header` → `Cls.Widget`/`Cls.WidgetTitle`,
`input-group`→`Cls.SearchBox`, `form-control`→`Cls.Field`, `badge`→`Cls.Tag`.

- [ ] **Step 1: Write the failing test (both modes)**

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Blazor.Components.Pages;
using Xunit;

namespace Postnomic.Client.Blazor.Tests;

public class MarkupStyleTests : TestContext
{
    private void Wire(PostnomicMarkupStyle style)
    {
        var svc = new Mock<IPostnomicBlogService>();
        svc.Setup(s => s.GetBlogAsync(default)).ReturnsAsync(new PostnomicBlogInfo { Name = "Blog", Slug = "b" });
        svc.Setup(s => s.GetPostsAsync(1, 5, null, null, null, null, null, default)).ReturnsAsync(
            new PostnomicPagedResult<PostnomicPostSummary>
            {
                Items = [ new PostnomicPostSummary { Slug="p", Title="Hello", AuthorName="A",
                    PublishedAt=DateTime.UtcNow, Language="en", AvailableLanguages=["en"] } ],
                Page=1, PageSize=5, TotalCount=1, TotalPages=1
            });
        // sidebar getters → empty
        svc.Setup(s => s.GetTagsAsync(default)).ReturnsAsync([]);
        svc.Setup(s => s.GetCategoriesAsync(default)).ReturnsAsync([]);
        svc.Setup(s => s.GetAuthorsAsync(default)).ReturnsAsync([]);
        svc.Setup(s => s.GetTopCommentedPostsAsync(It.IsAny<int>(), default)).ReturnsAsync([]);
        svc.Setup(s => s.GetMostReadPostsAsync(It.IsAny<int>(), default)).ReturnsAsync([]);
        Services.AddSingleton(svc.Object);
        Services.AddSingleton(Options.Create(new PostnomicClientOptions
        { BaseUrl="https://api.x", ApiKey="k", BlogSlug="b", BasePath="/blog", MarkupStyle=style }));
    }

    [Fact]
    public void Default_bootstrap_mode_still_emits_bootstrap()
    {
        Wire(PostnomicMarkupStyle.Bootstrap);
        var html = RenderComponent<BlogPage>().Markup;
        html.Should().Contain("card").And.Contain("col-lg-8");
        html.Should().NotContain("pn-card");
    }

    [Fact]
    public void Semantic_mode_emits_pn_and_no_bootstrap()
    {
        Wire(PostnomicMarkupStyle.Semantic);
        var html = RenderComponent<BlogPage>().Markup;
        html.Should().Contain("pn-blog").And.Contain("pn-card").And.Contain("pn-post-title");
        foreach (var bs in new[] { "col-lg-", "card mb-4", "badge", "btn btn-", "bi bi-" })
            html.Should().NotContain(bs);
    }
}
```

- [ ] **Step 2: Run — expect FAIL** (Semantic assertions fail; classes still hard-coded)

Run: `dotnet test tests/Postnomic.Client.Blazor.Tests --filter MarkupStyleTests`
Expected: FAIL.

- [ ] **Step 3: Refactor** every Blazor component to resolve classes through `Cls` (pattern above).

- [ ] **Step 4: Run — expect PASS (both modes)**

Run: `dotnet test tests/Postnomic.Client.Blazor.Tests`
Expected: PASS (full suite; existing tests still green).

- [ ] **Step 5: Commit**

```bash
git add src/Postnomic.Client.Blazor/Components tests/Postnomic.Client.Blazor.Tests/MarkupStyleTests.cs
git commit -m "feat(blazor): components honor opt-in Semantic markup style"
```

---

### Task 4: AspNetCore Blog Area honors `MarkupStyle`

**Files:**
- Modify: `src/Postnomic.Client.AspNetCore/Areas/Blog/Pages/{Index,Post,Author}.cshtml` (+ `.cshtml.cs`), `_Comment.cshtml`
- Test: `tests/Postnomic.Client.AspNetCore.Tests/MarkupStyleRenderingTests.cs`

**Interfaces:**
- Consumes: `PostnomicCssClasses` (Task 1). The page models already resolve
  `PostnomicClientOptions` (via `IOptionsMonitor`/resolver) to compute `BasePath`,
  `IsMasonry`, etc. — expose a `public PostnomicCssClasses Cls { get; private set; }`
  built from the resolved `options.MarkupStyle`, set in `OnGetAsync`, and use
  `@Model.Cls.Card` in the cshtml.

**Pattern:** in each page model, where the resolved `PostnomicClientOptions` is already
obtained, add `Cls = new PostnomicCssClasses(resolvedOptions.MarkupStyle);`. In the
cshtml, replace class literals with `@Model.Cls.<Role>`. Icons: Bootstrap mode keeps
`<i class="bi …">`, Semantic renders inline SVG via `@if (Model.Cls is …)` — expose a
`bool Semantic => …` helper on the model for the conditional.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Postnomic.Client.AspNetCore.Tests;

// Uses the test host that already boots the Blog area (see existing SeoRenderingTests /
// FeedEndpointTests for the WebApplicationFactory + mock IPostnomicBlogService setup).
public class MarkupStyleRenderingTests : IClassFixture<BlogAppFactory>
{
    private readonly BlogAppFactory _factory;
    public MarkupStyleRenderingTests(BlogAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Default_mode_emits_bootstrap_classes()
    {
        var client = _factory.WithMarkupStyle(PostnomicMarkupStyle.Bootstrap).CreateClient();
        var html = await client.GetStringAsync("/blog");
        html.Should().Contain("card").And.Contain("col-lg-8");
        html.Should().NotContain("pn-card");
    }

    [Fact]
    public async Task Semantic_mode_emits_pn_classes()
    {
        var client = _factory.WithMarkupStyle(PostnomicMarkupStyle.Semantic).CreateClient();
        var html = await client.GetStringAsync("/blog");
        html.Should().Contain("pn-card");
        html.Should().NotContain("col-lg-8");
    }
}
```
(If the existing test factory doesn't parameterize options, add a `WithMarkupStyle`
helper that reconfigures `PostnomicClientOptions.MarkupStyle` via
`ConfigureTestServices` + `PostConfigure<PostnomicClientOptions>`.)

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test tests/Postnomic.Client.AspNetCore.Tests --filter MarkupStyleRenderingTests`
Expected: FAIL.

- [ ] **Step 3: Refactor** the Blog Area page models + cshtml to resolve classes through `Model.Cls`.

- [ ] **Step 4: Run — full AspNetCore suite green (Xircuit regression: default unchanged)**

Run: `dotnet test tests/Postnomic.Client.AspNetCore.Tests`
Expected: PASS (existing SEO/route/render tests still green — they assert the default-mode Bootstrap output).

- [ ] **Step 5: Commit**

```bash
git add src/Postnomic.Client.AspNetCore/Areas tests/Postnomic.Client.AspNetCore.Tests/MarkupStyleRenderingTests.cs
git commit -m "feat(aspnetcore): Blog Area honors opt-in Semantic markup style"
```

---

### Task 5: Host-agnostic feed builder (reusable from Blazor)

**Files:**
- Create: `src/Postnomic.Client.Abstractions/Seo/PostnomicFeedBuilder.cs`
- Modify: `src/Postnomic.Client.AspNetCore/Seo/PostnomicFeeds.cs` (delegate; output unchanged)
- Test: `tests/Postnomic.Client.Abstractions.Tests/PostnomicFeedBuilderTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace Postnomic.Client.Abstractions.Seo;
  public static class PostnomicFeedBuilder
  {
      public static Task<string> BuildSitemapAsync(IPostnomicBlogService blog, string absoluteBaseUrl,
          string basePath, PostnomicLanguageRouteStyle style, CancellationToken ct = default);
      public static Task<string> BuildRssAsync(IPostnomicBlogService blog, string absoluteBaseUrl,
          string basePath, PostnomicLanguageRouteStyle style, string channelTitle,
          string? channelDescription, CancellationToken ct = default);
  }
  ```
- Consumed by: OutaStory Plan Tasks 3 (`/blog/rss.xml`) and 7 (sitemap fold-in).

**Approach:** Port the XML logic from `PostnomicFeeds` into `PostnomicFeedBuilder`,
replacing `HttpRequest request` with `string absoluteBaseUrl` and the
`ToAbsoluteUrl(request, path)` calls with `absoluteBaseUrl.TrimEnd('/') + path` (return
`path` as-is if already absolute http(s)). `PostnomicFeeds` computes
`$"{request.Scheme}://{request.Host}"` and delegates — byte-identical output.

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Abstractions.Seo;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

public class PostnomicFeedBuilderTests
{
    private static Mock<IPostnomicBlogService> Svc()
    {
        var m = new Mock<IPostnomicBlogService>();
        m.Setup(s => s.GetPostsAsync(It.IsAny<int>(), It.IsAny<int>(), null, null, null, null, null, default))
         .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
         {
             Items = [ new PostnomicPostSummary { Slug="hello", Title="Hello", Language="en",
                 AvailableLanguages=["en"], PublishedAt=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc) } ],
             Page=1, PageSize=50, TotalCount=1, TotalPages=1
         });
        return m;
    }

    [Fact]
    public async Task Sitemap_uses_absolute_base_url()
    {
        var xml = await PostnomicFeedBuilder.BuildSitemapAsync(
            Svc().Object, "https://www.outastory.com", "/blog", PostnomicLanguageRouteStyle.None);
        xml.Should().Contain("https://www.outastory.com/blog/post/hello").And.NotContain("file:");
    }

    [Fact]
    public async Task Rss_uses_absolute_base_url_and_channel_title()
    {
        var xml = await PostnomicFeedBuilder.BuildRssAsync(
            Svc().Object, "https://www.outastory.com", "/blog", PostnomicLanguageRouteStyle.None,
            "OutaStory | Blog", "desc");
        xml.Should().Contain("<title>OutaStory | Blog</title>").And.Contain("https://www.outastory.com/blog/post/hello");
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test tests/Postnomic.Client.Abstractions.Tests --filter PostnomicFeedBuilderTests`
Expected: FAIL (type not found).

- [ ] **Step 3: Create `PostnomicFeedBuilder`** (ported logic) and rewrite `PostnomicFeeds` to delegate.

- [ ] **Step 4: Run — both projects green**

Run: `dotnet test tests/Postnomic.Client.Abstractions.Tests --filter PostnomicFeedBuilderTests` → PASS
Run: `dotnet test tests/Postnomic.Client.AspNetCore.Tests --filter Feed` → PASS (unchanged output)

- [ ] **Step 5: Commit**

```bash
git add src/Postnomic.Client.Abstractions/Seo/PostnomicFeedBuilder.cs src/Postnomic.Client.AspNetCore/Seo/PostnomicFeeds.cs tests/Postnomic.Client.Abstractions.Tests/PostnomicFeedBuilderTests.cs
git commit -m "feat(abstractions): host-agnostic sitemap/RSS feed builder"
```

---

### Task 6: Docs + release v1.3.0

**Files:**
- Modify: root/README + each package README — document `MarkupStyle`, the `--pn-*`
  theming contract, the stylesheet include paths, and a host override snippet.
- Modify: `CHANGELOG.md` if present.

- [ ] **Step 1: Full solution build + test (Release)**

Run: `dotnet test Postnomic.Client.slnx -c Release`
Expected: all PASS, coverage ≥80%.

- [ ] **Step 2: Document** `MarkupStyle` (default Bootstrap, opt-in Semantic), the
  `--pn-*` variables, include paths (`_content/Postnomic.Client.{Blazor,AspNetCore}/postnomic-blog.css`),
  and `.pn-blog { --pn-primary: var(--brand) }` override example.

- [ ] **Step 3: Commit docs**

```bash
git add . && git commit -m "docs: MarkupStyle + pn-* theming contract for v1.3.0"
```

- [ ] **Step 4: Tag + push + release**

```bash
git push origin main
git tag v1.3.0 && git push origin v1.3.0
gh release create v1.3.0 --generate-notes
```
Expected: `publish.yml` → `publish-github` pushes prerelease; `publish-nuget` waits at the `nuget-publish` gate.

- [ ] **Step 5: Request operator approval** of the `nuget-publish` gate; confirm v1.3.0 on NuGet.org.

- [ ] **Step 6: Bump parent submodule pointer** (`threeb-it/Postnomic` root):

```bash
cd ../..
git add lib/postnomic-dotnet
git commit -m "chore: bump postnomic-dotnet submodule to v1.3.0"
git push
```

---

## Self-Review

- **Spec coverage:** A.1 switch+resolver → Task 1; A.2 both packages honor it → Tasks 3 (Blazor) + 4 (AspNetCore); A.3 shared stylesheet → Task 2; A.4 feed reuse → Task 5; A.5 tests → in every task (incl. default-mode regression guards); A.6 release → Task 6. ✅
- **Placeholders:** none — the class table gives concrete Bootstrap↔Semantic values (with an instruction to verify each literal against the file); resolver signature, CSS contract, and feed signature are explicit.
- **Type consistency:** `PostnomicMarkupStyle`, `MarkupStyle`, `PostnomicCssClasses` roles (Task 1) reused verbatim in Tasks 3–4; `PostnomicFeedBuilder` signature (Task 5) matches OutaStory Plan Tasks 3 & 7.
- **No-break guard:** every markup task asserts default (Bootstrap) output is unchanged; existing AspNetCore render/SEO tests stay green → Xircuit safe.
