# Postnomic.Client.AspNetCore

Drop-in Razor Pages Area that adds a fully-featured blog to any ASP.NET Core application. Register
one service call, add one line to your layout, and you get an index page, post detail page,
author page, threaded comments, hreflang-aware multi-language routing, full SEO output, and a
sitemap/RSS feed -- all without writing any UI code.

```bash
dotnet add package Postnomic.Client.AspNetCore
```

## Quick Start

```csharp
// Program.cs
using Postnomic.Client.Abstractions;
using Postnomic.Client.AspNetCore;

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
```

The Blog area's pages are discovered automatically once `Postnomic.Client.AspNetCore` is
referenced and Razor Pages are enabled -- your blog is live at `/blog` (or wherever you set
`BasePath`) with no additional page files.

### Required: render the `PostnomicHead` section

The Blog area pages emit their SEO tags (canonical, hreflang, OpenGraph, Twitter Card, JSON-LD)
into a Razor section named `PostnomicHead`. Your host layout (`Pages/Shared/_Layout.cshtml` or
equivalent) must render that section inside `<head>`, or none of those tags will appear:

```cshtml
<head>
    <meta charset="utf-8" />
    <title>@ViewData["Title"]</title>
    ...
    @await RenderSectionAsync("PostnomicHead", required: false)
</head>
```

Use `required: false` -- the rest of your application's pages don't define this section.

## `AddPostnomicBlog`

Registers `IPostnomicBlogService`, the underlying `HttpClient`, the Blog area's page routes, and
(for named registrations) a keyed service so multiple blogs can coexist:

```csharp
// Default (unnamed) blog
builder.Services.AddPostnomicBlog(options =>
{
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";
    options.BasePath = "/blog";                                  // default
    options.LanguageRouteStyle = PostnomicLanguageRouteStyle.Suffix; // default
});

// Named blog (host multiple blogs in one app)
builder.Services.AddPostnomicBlog("engineering", options =>
{
    options.BlogSlug = "engineering-blog";
    options.ApiKey = "pk_live_eng_...";
    options.BaseUrl = "https://api.postnomic.com";
    options.BasePath = "/engineering";
});
```

Call `services.AddRazorPages()` (or `AddControllersWithViews()`) yourself so the Area pages are
discovered.

## Routes

For `LanguageRouteStyle.Suffix` (the default), each registered blog gets:

| Page | Route(s) |
|---|---|
| Index | `{basePath}` and `{basePath}/{lang}` |
| Post | `{basePath}/post/{postSlug}` and `{basePath}/{lang}/post/{postSlug}` |
| Author | `{basePath}/author/{authorSlug}` and `{basePath}/{lang}/author/{authorSlug}` |

`{lang}` is constrained to exactly two lowercase letters. `LanguageRouteStyle.Prefix` moves the
language segment before the base path (`/{lang}/blog/...`, with no bare fallback route for
non-default languages); `LanguageRouteStyle.None` never registers a language-segment route at all.
See the [root README](../../README.md#language-route-style) for the full URL-shape table and a
Prefix-mode example.

## `MapPostnomicBlog()` -- sitemap.xml, rss.xml

Call this after `app.MapRazorPages()`. For every blog registered via `AddPostnomicBlog` it adds:

- `GET {basePath}/sitemap.xml` -- the index page plus every post (paged internally, up to 2,000
  posts), each with an `xhtml:link` hreflang alternate per available language
- `GET {basePath}/rss.xml` -- the 20 most recent posts as an RSS 2.0 feed

```csharp
app.MapRazorPages();
app.MapPostnomicBlog();
```

### `MapPostnomicRobots()` (optional)

Serves `GET /robots.txt` (`Allow: /` plus a `Sitemap:` directive per registered blog). Opt-in --
only call it if your app doesn't already serve its own `/robots.txt`:

```csharp
app.MapPostnomicBlog();
app.MapPostnomicRobots();
```

## SEO

`PostnomicSeoBuilder` (from `Postnomic.Client.Abstractions`) builds a `PostnomicSeoModel` per page,
rendered by the `_SeoHead.cshtml` partial into the `PostnomicHead` section described above:

- Self-referential canonical URL per language
- Meta description (from the post excerpt, or a stripped/truncated content snippet)
- `robots` meta tag
- hreflang alternates for every available language, plus `x-default`
- OpenGraph (`og:type`, `og:title`, `og:description`, `og:url`, `og:image`, `og:site_name`,
  `og:locale`) and `article:*` tags on post pages
- Twitter Card (`summary_large_image`)
- JSON-LD `@graph`: `BlogPosting` (post), `Blog` + `ItemList` (index), or `ProfilePage` (author),
  plus a `BreadcrumbList`

The `PostModel` page also still exposes `CanonicalUrl` and `AlternateLanguageUrls` (relative-path,
low-level) if you need to build additional custom tags -- but the automatic head above already
covers the standard set.

## Theming / MarkupStyle

`PostnomicClientOptions.MarkupStyle` (default `Bootstrap`) selects the CSS class vocabulary the
Blog area pages emit. Set it to `PostnomicMarkupStyle.Semantic` to render framework-free `pn-*`
classes instead of Bootstrap utility classes, and include the shipped stylesheet in your layout's
`<head>`:

```csharp
builder.Services.AddPostnomicBlog(options =>
{
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";
    options.MarkupStyle = PostnomicMarkupStyle.Semantic;
});
```

```cshtml
@* Pages/Shared/_Layout.cshtml *@
<head>
    ...
    <link rel="stylesheet" href="_content/Postnomic.Client.AspNetCore/postnomic-blog.css" />
</head>
```

See the [root README](../../README.md#theming--markupstyle) for the full `--pn-*` variable
reference and a `.pn-blog { --pn-primary: ...; }` override example -- rebrand the blog entirely
through CSS variables, no page/class overrides needed.

## Multi-Blog Support

Call `AddPostnomicBlog(name, configure)` once per blog with distinct `BasePath` values; each gets
its own routes (and, once `MapPostnomicBlog()` is called, its own sitemap/RSS):

```csharp
builder.Services.AddPostnomicBlog("free", options =>
{
    options.BlogSlug = "free-tier-blog";
    options.BasePath = "/blog/free";
    // ...
});

builder.Services.AddPostnomicBlog("enterprise", options =>
{
    options.BlogSlug = "enterprise-blog";
    options.BasePath = "/blog/enterprise";
    // ...
});
```

## Requirements

- .NET 10.0 or later
- `services.AddRazorPages()` (or `AddControllersWithViews()`) called in the host app
- A `PostnomicHead` section rendered in the host layout's `<head>` (see above)

## Links

- [Root SDK README](../../README.md) -- full options reference, multi-language posts, and more
- [Postnomic Website](https://www.postnomic.com)
- [Report an Issue](https://github.com/threeb-it/postnomic-dotnet/issues)
