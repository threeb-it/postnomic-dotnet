# Postnomic .NET Client SDK

The official .NET Client SDK for [Postnomic](https://www.postnomic.com) -- the developer-first headless blog backend. Add a fully-featured blog to any .NET application with a single NuGet package and a few lines of code.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

## Why Postnomic?

[Postnomic](https://www.postnomic.com) gives you everything you need to run a blog -- content management, analytics, comments, multi-user collaboration, media hosting -- all exposed through a clean REST API. You build the frontend; we handle the backend.

- **REST API** with full OpenAPI documentation
- **Multi-blog** support with team roles and permissions
- **Built-in analytics**, comment moderation, and content scheduling
- **EU-hosted** infrastructure with GDPR compliance
- **Free tier** to get started -- no credit card required

Sign up at [www.postnomic.com](https://www.postnomic.com) and have your blog backend running in minutes.

## Packages

| Package | Description | NuGet |
|---|---|---|
| **Postnomic.Client.Abstractions** | Interfaces, DTOs, and configuration options | [![NuGet](https://img.shields.io/nuget/v/Postnomic.Client.Abstractions)](https://www.nuget.org/packages/Postnomic.Client.Abstractions) |
| **Postnomic.Client** | HTTP client implementation with optional caching | [![NuGet](https://img.shields.io/nuget/v/Postnomic.Client)](https://www.nuget.org/packages/Postnomic.Client) |
| **Postnomic.Client.AspNetCore** | Drop-in Razor Pages Area for ASP.NET Core apps | [![NuGet](https://img.shields.io/nuget/v/Postnomic.Client.AspNetCore)](https://www.nuget.org/packages/Postnomic.Client.AspNetCore) |
| **Postnomic.Client.Blazor** | Blazor components for Server and WebAssembly | [![NuGet](https://img.shields.io/nuget/v/Postnomic.Client.Blazor)](https://www.nuget.org/packages/Postnomic.Client.Blazor) |

## Quick Start

### ASP.NET Core (Razor Pages / MVC)

```bash
dotnet add package Postnomic.Client.AspNetCore
```

```csharp
// Program.cs
builder.Services.AddPostnomicBlog(options =>
{
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";
});
builder.Services.AddRazorPages();

var app = builder.Build();
// ...
app.MapRazorPages();
app.MapPostnomicBlog(); // also serves /blog/sitemap.xml and /blog/rss.xml

app.Run();

// That's it -- your blog is live at /blog
```

Add one line to your host layout's `<head>` (e.g. `Pages/Shared/_Layout.cshtml`) so the SDK can
inject its per-page canonical/OpenGraph/JSON-LD tags -- see [SEO](#seo) below:

```cshtml
<head>
    ...
    @await RenderSectionAsync("PostnomicHead", required: false)
</head>
```

### Blazor (Server / WebAssembly)

```bash
dotnet add package Postnomic.Client.Blazor
```

```csharp
// Program.cs
builder.Services.AddPostnomicBlog(options =>
{
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";
});
```

### HTTP Client Only

If you want full control over rendering, use the base client package:

```bash
dotnet add package Postnomic.Client
```

```csharp
builder.Services.AddPostnomicClient(options =>
{
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";
});

// Inject IPostnomicBlogService anywhere
public class MyController(IPostnomicBlogService blog)
{
    public async Task<IActionResult> Index()
    {
        var posts = await blog.GetPostsAsync();
        return View(posts);
    }
}
```

## Configuration

All packages are configured through `PostnomicClientOptions`:

```csharp
builder.Services.AddPostnomicBlog(options =>
{
    // Required
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";

    // Optional: customize the blog URL path (default: /blog)
    options.BasePath = "/articles";

    // Optional: where the language code appears in URLs for translated posts (default: Suffix)
    options.LanguageRouteStyle = PostnomicLanguageRouteStyle.Suffix;

    // Optional: enable client-side caching
    options.Cache = new PostnomicCacheOptions
    {
        Enabled = true,
        PostListDuration = TimeSpan.FromMinutes(5),
        PostDetailDuration = TimeSpan.FromMinutes(10),
    };
});
```

### Options reference

| Option | Type | Default | Description |
|---|---|---|---|
| `BlogSlug` | `string` | `""` | Required. URL-friendly slug of the blog this client targets. |
| `ApiKey` | `string` | `""` | Required. Sent as the `X-Api-Key` header on every request. |
| `BaseUrl` | `string` | `""` | Required. Base URL of the Postnomic API (no trailing slash). |
| `BasePath` | `string` | `/blog` | Base path the blog is served at (Razor Pages) or linked under (Blazor). |
| `ShowBranding` | `bool` | `false` | Renders a "Powered by Postnomic" footer; server-enforced value from your plan takes precedence. |
| `LanguageRouteStyle` | `PostnomicLanguageRouteStyle` | `Suffix` | Where the language code appears in generated URLs. See [Language route style](#language-route-style) below. |
| `MarkupStyle` | `PostnomicMarkupStyle` | `Bootstrap` | CSS class vocabulary emitted by Postnomic-rendered markup. See [Theming / MarkupStyle](#theming--markupstyle) below. |
| `Cache` | `PostnomicCacheOptions?` | `null` | Optional client-side in-memory caching. |
| `AlternateUrlResolver` | `Func<...>?` | `null` | **Obsolete** -- superseded by `IPostnomicAlternateUrlProvider`. See [Per-post hreflang alternates](docs/hreflang-alternates.md). |

> **Every SDK service takes `IOptions<PostnomicClientOptions>`** -- `PostnomicBlogService`,
> `CachingPostnomicBlogService`, `PostnomicAuthoringService`, both auth handlers, and the typed
> `HttpClient` registrations behind them. So **no options callback may depend on a service that
> touches the SDK**: configuring options with the DI-aware `OptionsBuilder.Configure<TDep>`
> overload self-recurses and throws
> `ValueFactory attempted to access the Value property of this instance.`
> Full reference: [Client options](docs/client-options.md).

### Multi-Blog Support

Host multiple blogs in a single application using named registrations:

```csharp
builder.Services.AddPostnomicBlog("engineering", options =>
{
    options.BlogSlug = "engineering-blog";
    options.ApiKey = "pk_live_eng_...";
    options.BasePath = "/engineering";
});

builder.Services.AddPostnomicBlog("product", options =>
{
    options.BlogSlug = "product-updates";
    options.ApiKey = "pk_live_prod_...";
    options.BasePath = "/product";
});
```

## Features

The SDK gives you access to the full Postnomic API:

- **Posts** -- list, filter by tag/category, full-text search, pagination
- **Post Detail** -- full HTML content, metadata, author info, related posts
- **Comments** -- threaded comments with configurable required fields
- **Tags & Categories** -- full taxonomy support
- **Authors** -- profiles with bio, social links, certifications, education
- **Popular Posts** -- trending content based on analytics
- **Blog Info** -- blog metadata, layout, and configuration
- **Multi-language posts** -- request a specific translation, get `/{lang}/` routes and hreflang metadata for free (see below)
- **Automatic SEO** -- canonical, hreflang, OpenGraph, Twitter Card, and JSON-LD structured data on every blog page (see [SEO](#seo))
- **Sitemap & RSS** -- `sitemap.xml` and `rss.xml` for every registered blog via `MapPostnomicBlog()` (see [Sitemap, RSS & robots.txt](#sitemap-rss--robotstxt))
- **Client-Side Caching** -- optional in-memory cache with per-resource TTLs and explicit invalidation via `IPostnomicCacheControl`
- **Theming** -- opt into framework-free `pn-*` classes and a shipped `--pn-*` variable-driven stylesheet instead of Bootstrap (see [Theming / MarkupStyle](#theming--markupstyle))

## Multi-language posts

If a blog has posts translated into multiple languages, the SDK lets you request a specific language and exposes what's available so you can build language switchers and SEO metadata.

### Requesting a language

`GetPostsAsync` and `GetPostAsync` both take an optional trailing `language` argument -- an ISO-639-1 code (e.g. `"de"`). It's sent to the API as `?lang=`, and it's part of the cache key when client-side caching is enabled. Leave it `null` to get the blog's default language (or let the API resolve it from the `Accept-Language` header).

```csharp
// Explicit language
var post = await blog.GetPostAsync("intro-to-docker", language: "de");

// Post list in a specific language
var posts = await blog.GetPostsAsync(language: "de");
```

`PostnomicPostSummary` and `PostnomicPostDetail` both expose:

- `Language` -- the language actually served for this post (may differ from what you requested if no translation exists; the API falls back to the blog's default language rather than 404ing)
- `AvailableLanguages` -- every language this post has content in

### Language route style

`{lang}` is always constrained to exactly two lowercase letters (e.g. `de`, `en`). `PostnomicClientOptions.LanguageRouteStyle` (`Postnomic.Client.Abstractions.PostnomicLanguageRouteStyle`) controls where that segment appears in generated URLs -- for both `Postnomic.Client.AspNetCore` route templates and every link the SDK generates (`PostnomicRouteBuilder`, sitemap/RSS, hreflang alternates):

| `LanguageRouteStyle` | Index | Post |
|---|---|---|
| `Suffix` (default) | `/blog`, `/blog/{lang}` | `/blog/post/{slug}`, `/blog/{lang}/post/{slug}` |
| `Prefix` | `/{lang}/blog` (only) | `/{lang}/blog/post/{slug}` (only) |
| `None` | `/blog` (only) | `/blog/post/{slug}` (only) |

- **`Suffix`** (default) preserves pre-1.2 behavior exactly: the default-language page is served bare, and every other language is available at a `{basePath}/{lang}` suffix.
- **`Prefix`** puts `{lang}` before the base path (`/de/blog/...`). Under `Prefix`, every URL is language-prefixed; there is no bare `/blog` route.
- **`None`** never emits or accepts a language segment; the API's own language resolution (`?lang=` query -> `Accept-Language` header -> blog default) decides what's served at the single bare route.

```csharp
// Program.cs -- Prefix mode: /de/blog, /de/blog/post/{slug}, etc.
builder.Services.AddPostnomicBlog(options =>
{
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";
    options.LanguageRouteStyle = PostnomicLanguageRouteStyle.Prefix;
});
```

### Blazor components

`PostPage`, `BlogPage`, and `AuthorPage` (`Postnomic.Client.Blazor`) all accept an optional `Language` parameter, which you bind to your own routed `{lang}` segment, plus honor the blog's configured `LanguageRouteStyle` when generating internal links (index, author, sidebar widgets).

## SEO

Every Blog area page (`Postnomic.Client.AspNetCore`) and every Blazor page component (`Postnomic.Client.Blazor`) automatically emits a full SEO head for you, built by the shared `PostnomicSeoBuilder`:

- **Canonical URL** -- self-referential per language (the `de` variant of a post canonicalizes to its own `/de/...` URL, not the default-language one)
- **Meta description** -- from the post excerpt, falling back to a stripped/truncated content snippet
- **`robots`** meta tag
- **hreflang alternates** -- one `<link rel="alternate">` per language the post/blog is available in, plus `x-default`
- **OpenGraph** -- `og:type`, `og:title`, `og:description`, `og:url`, `og:image`, `og:site_name`, `og:locale` (`de_DE`/`en_US`/...), and `article:published_time` / `article:author` / `article:tag` on post pages
- **Twitter Card** -- `summary_large_image`
- **JSON-LD** -- a `@graph` of `BlogPosting` (post pages), `Blog` + `ItemList` (index), or `ProfilePage` (author pages), plus a `BreadcrumbList` on every page type

### Per-post hreflang alternates

The SDK composes hreflang alternates by applying your `LanguageRouteStyle` to the post's own slug.
That is correct only when every translation shares the original's slug -- and a translated slug is
**not** derivable from the original's. It may be identical, suffixed, or fully translated
(`kurze-hoerbuecher` -> `short-audiobooks`).

When your translations don't all share one slug, supply the real URLs by implementing
`IPostnomicAlternateUrlProvider`:

```csharp
public sealed class BlogAlternateUrlProvider(IPostnomicBlogService blog)
    : IPostnomicAlternateUrlProvider
{
    public async ValueTask<IReadOnlyList<(string Language, string Url)>?> GetAlternatesAsync(
        PostnomicPostDetail post, CancellationToken cancellationToken = default)
    {
        var alternates = new List<(string Language, string Url)>();
        foreach (var language in post.AvailableLanguages)
        {
            var translated = await blog.GetPostAsync(post.Slug, language, cancellationToken);
            if (translated is not null)
                alternates.Add((language, $"/blog/post/{translated.Slug}"));
        }

        return alternates.Count > 0 ? alternates : null;
    }
}
```

```csharp
builder.Services.AddPostnomicAlternateUrlProvider<BlogAlternateUrlProvider>();
```

The SDK resolves it from DI at render time, so it may depend on `IPostnomicBlogService` -- unlike an
options callback, which cannot. It's async, so no cache-warming pass is needed. It works identically
in both hosting models.

Full guide: **[Per-post hreflang alternates](docs/hreflang-alternates.md)**.

### ASP.NET Core (Razor Pages)

The Blog area pages render their SEO tags into a Razor section named **`PostnomicHead`**. Your host layout must render that section inside `<head>`, or none of the tags above will appear on the page:

```cshtml
@* Pages/Shared/_Layout.cshtml *@
<head>
    <meta charset="utf-8" />
    ...
    @await RenderSectionAsync("PostnomicHead", required: false)
</head>
```

`required: false` is important -- non-blog pages in your app don't define the section.

The low-level `CanonicalUrl` / `AlternateLanguageUrls` properties on `PostModel` are still available (they're relative-path, not absolute, and predate the automatic SEO head) if you need to build your own custom tags, but for the standard tag set above you don't need to touch them.

### Blazor

Blazor needs no extra wiring beyond the `<HeadOutlet />` every Blazor app's root component already has -- `PostPage`/`BlogPage`/`AuthorPage` render their SEO tags via `<HeadContent>`, which `<HeadOutlet />` picks up automatically.

## Theming / MarkupStyle

By default, every Postnomic-rendered page (`Postnomic.Client.AspNetCore` Razor Pages and every
`Postnomic.Client.Blazor` component) emits **Bootstrap** utility classes (`card`, `row`, `btn
btn-primary`, ...) -- this is `PostnomicMarkupStyle.Bootstrap`, the default, and it preserves
pre-1.3 output byte-for-byte for existing consumers.

Opt into **`PostnomicMarkupStyle.Semantic`** to render framework-free `pn-*` classes instead,
themed entirely through CSS custom properties:

```csharp
builder.Services.AddPostnomicBlog(options =>
{
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";
    options.MarkupStyle = PostnomicMarkupStyle.Semantic;
});
```

Both packages ship a ready-to-use stylesheet that styles every `pn-*` class purely from `--pn-*`
variables. Include it once in your host layout's `<head>` (pick the package you actually
reference -- only load one):

```html
<link rel="stylesheet" href="_content/Postnomic.Client.AspNetCore/postnomic-blog.css" />
<!-- or, in a Blazor app: -->
<link rel="stylesheet" href="_content/Postnomic.Client.Blazor/postnomic-blog.css" />
```

Rebrand the blog by overriding `--pn-*` variables under `.pn-blog` (the outermost container in
Semantic mode) in your own stylesheet, loaded *after* `postnomic-blog.css`:

```css
.pn-blog {
    --pn-primary: var(--brand);
    --pn-on-primary: #ffffff;
    --pn-font-heading: "Poppins", sans-serif;
    --pn-radius-lg: 4px;
}
```

Every variable the stylesheet declares (with its shipped default, on `.pn-blog`):

| Variable | Purpose |
|---|---|
| `--pn-font` | Base font stack |
| `--pn-font-heading` | Heading font stack (titles) |
| `--pn-max-width` | Max width of the blog container |
| `--pn-surface` | Card/widget background |
| `--pn-surface-variant` | Secondary surface (tag pills, filter banner, code blocks) |
| `--pn-text` | Primary text color |
| `--pn-text-muted` | Secondary/muted text color |
| `--pn-primary` | Brand/accent color (primary buttons, active pagination, category tags) |
| `--pn-on-primary` | Text/icon color on top of `--pn-primary` |
| `--pn-border` | Border color used throughout |
| `--pn-link` | Link color (defaults to `--pn-primary`) |
| `--pn-radius` | Small corner radius (buttons, tags, fields) |
| `--pn-radius-lg` | Large corner radius (cards, widgets) |
| `--pn-space-xs` / `--pn-space-sm` / `--pn-space-md` / `--pn-space-lg` / `--pn-space-xl` | Spacing scale used for gaps, padding, and margins |

`PostnomicMarkupStyle.Bootstrap` mode does **not** load or need `postnomic-blog.css` -- keep
styling it with your own Bootstrap theme/overrides as before.

## Sitemap, RSS & robots.txt

Call `app.MapPostnomicBlog()` after `app.MapRazorPages()` to also serve, for every blog registered via `AddPostnomicBlog` (the default blog and every named one):

- `GET {basePath}/sitemap.xml` -- every post plus the index page, with `xhtml:link` hreflang alternates per post
- `GET {basePath}/rss.xml` -- the 20 most recent posts as an RSS 2.0 feed

```csharp
app.MapRazorPages();
app.MapPostnomicBlog();
```

Optionally, call `app.MapPostnomicRobots()` to serve a `GET /robots.txt` that allows all crawling and lists a `Sitemap:` directive for every registered blog. This is opt-in: only call it if your host app doesn't already serve its own `/robots.txt` (mapping both registers two competing handlers for the same route).

```csharp
app.MapPostnomicBlog();
app.MapPostnomicRobots();
```

These endpoints are ASP.NET Core-only (`Postnomic.Client.AspNetCore`); there is no Blazor equivalent.

## Requirements

- .NET 10.0 or later
- A Postnomic account ([sign up free](https://www.postnomic.com))
- An API key from your Postnomic dashboard

## Project Structure

```text
src/
  Postnomic.Client.Abstractions/   # Interfaces and DTOs (no dependencies)
  Postnomic.Client/                # HTTP client implementation
  Postnomic.Client.AspNetCore/     # Razor Pages integration
  Postnomic.Client.Blazor/         # Blazor component integration
tests/
  Postnomic.Client.Abstractions.Tests/
  Postnomic.Client.Tests/
  Postnomic.Client.AspNetCore.Tests/
  Postnomic.Client.Blazor.Tests/
```

## Development

```bash
# Build
dotnet build Postnomic.Client.slnx

# Run tests
dotnet test Postnomic.Client.slnx

# Pack NuGet packages
dotnet pack Postnomic.Client.slnx -c Release
```

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

This project is licensed under the MIT License -- see the [LICENSE](LICENSE) file for details.

## Links

- [Postnomic Website](https://www.postnomic.com)
- [API Documentation](https://www.postnomic.com/Support)
- [Report an Issue](https://github.com/threeb-it/postnomic-dotnet/issues)

### Guides

- [Per-post hreflang alternates](docs/hreflang-alternates.md) -- translated slugs, the provider seam, multi-blog
- [Client options reference](docs/client-options.md) -- every option, and the DI constraint on options callbacks
- [Migration 1.8 -> 1.9](docs/migration-1.8-to-1.9.md) -- what's obsolete and what replaces it
- [Troubleshooting](docs/troubleshooting.md) -- keyed on the exception text

---

Built with care by [ThreeB IT GmbH](https://www.threebit.io) in Ibbenbueren, Germany.
