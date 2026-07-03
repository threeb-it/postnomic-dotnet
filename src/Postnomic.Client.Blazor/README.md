# Postnomic.Client.Blazor

Blazor components (Server and WebAssembly) that add a fully-featured blog to any Blazor
application: a blog index, post detail, author page, threaded comments, hreflang-aware
multi-language routing, and full SEO output via Blazor's native `<HeadOutlet>`.

```bash
dotnet add package Postnomic.Client.Blazor
```

## Quick Start

```csharp
// Program.cs
using Postnomic.Client.Abstractions;
using Postnomic.Client.Blazor;

builder.Services.AddPostnomicBlog(options =>
{
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";
});
```

```razor
@* Your own routed page, e.g. Components/Pages/Blog.razor *@
@page "/blog"
@using Postnomic.Client.Blazor.Components.Pages

<BlogPage />
```

```razor
@* Components/Pages/BlogPost.razor *@
@page "/blog/post/{PostSlug}"
@using Postnomic.Client.Blazor.Components.Pages

<PostPage PostSlug="@PostSlug" />

@code {
    [Parameter] public string PostSlug { get; set; } = "";
}
```

`AddPostnomicBlog` only registers services -- you own the `@page` routes and decide where in your
app's navigation the blog lives.

## SEO -- no extra wiring required

`BlogPage`, `PostPage`, and `AuthorPage` each render a `PostnomicSeoHead` component internally,
which emits canonical/hreflang/OpenGraph/Twitter Card/JSON-LD tags via Blazor's `<HeadContent>`.
As long as your app's root component has the standard `<HeadOutlet />` (present in every Blazor
Web App template, e.g. `Components/App.razor`), those tags render automatically -- there's nothing
extra to add, unlike the ASP.NET Core package's `PostnomicHead` section requirement:

```razor
@* Components/App.razor *@
<head>
    ...
    <HeadOutlet />
</head>
```

The SEO output mirrors `Postnomic.Client.AspNetCore`'s `_SeoHead.cshtml` exactly (same
`PostnomicSeoModel`, same tags), so both hosting models produce identical `<head>` content for the
same blog:

- Self-referential canonical URL per language
- Meta description, `robots` meta tag
- hreflang alternates for every available language, plus `x-default`
- OpenGraph (incl. `og:locale` in `de_DE`/`en_US` form) and `article:*` tags on post pages
- Twitter Card (`summary_large_image`)
- JSON-LD `@graph`: `BlogPosting` (post), `Blog` + `ItemList` (index), or `ProfilePage` (author),
  plus a `BreadcrumbList`

## Language route style

`BlogPage`, `PostPage`, and `AuthorPage` all accept an optional `Language` parameter -- bind it to
your own routed `{lang}` segment -- and honor the blog's configured `LanguageRouteStyle` (default
`Suffix`) when generating internal links (back-to-blog, author links, sidebar widgets):

```razor
@page "/{Lang}/blog/post/{PostSlug}"
@using Postnomic.Client.Blazor.Components.Pages

<PostPage PostSlug="@PostSlug" Language="@Lang" />

@code {
    [Parameter] public string PostSlug { get; set; } = "";
    [Parameter] public string? Lang { get; set; }
}
```

```csharp
// Program.cs -- Prefix mode: pair with a "{Lang}/blog/..." route template above
builder.Services.AddPostnomicBlog(options =>
{
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";
    options.LanguageRouteStyle = PostnomicLanguageRouteStyle.Prefix;
});
```

See the [root README](../../README.md#language-route-style) for the full `LanguageRouteStyle`
URL-shape table (`Suffix`/`Prefix`/`None`).

## Theming / MarkupStyle

`PostnomicClientOptions.MarkupStyle` (default `Bootstrap`) applies to every Blazor component the
same way it does to `Postnomic.Client.AspNetCore` -- set it to `PostnomicMarkupStyle.Semantic` to
render framework-free `pn-*` classes instead of Bootstrap utility classes, and include the shipped
stylesheet in your root component's `<head>`:

```csharp
builder.Services.AddPostnomicBlog(options =>
{
    options.BlogSlug = "my-blog";
    options.ApiKey = "pk_live_...";
    options.BaseUrl = "https://api.postnomic.com";
    options.MarkupStyle = PostnomicMarkupStyle.Semantic;
});
```

```razor
@* Components/App.razor *@
<head>
    ...
    <link rel="stylesheet" href="_content/Postnomic.Client.Blazor/postnomic-blog.css" />
</head>
```

See the [root README](../../README.md#theming--markupstyle) for the full `--pn-*` variable
reference and a `.pn-blog { --pn-primary: ...; }` override example -- rebrand the blog entirely
through CSS variables, no component/class overrides needed.

## Sitemap & RSS (`PostnomicFeedBuilder`)

Unlike `Postnomic.Client.AspNetCore`, this package has no `MapPostnomicBlog()` endpoint mapper --
there's no Razor Pages Area routing here to hang a `sitemap.xml`/`rss.xml` GET off of. Use the
host-agnostic `PostnomicFeedBuilder` (`Postnomic.Client.Abstractions.Seo`) directly from a minimal
API endpoint in your `Program.cs` instead. It takes an already-resolved `absoluteBaseUrl` string
rather than an ASP.NET Core `HttpRequest`, so it works from any hosting model with no ambient
request to derive scheme+host from -- Blazor Server, WebAssembly-hosted, or otherwise:

```csharp
// Program.cs
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Seo;

app.MapGet("/blog/sitemap.xml", async (IPostnomicBlogService blog, IOptions<PostnomicClientOptions> options) =>
{
    var xml = await PostnomicFeedBuilder.BuildSitemapAsync(
        blog, "https://www.example.com", options.Value.BasePath, options.Value.LanguageRouteStyle);
    return Results.Content(xml, "application/xml");
});

app.MapGet("/blog/rss.xml", async (IPostnomicBlogService blog, IOptions<PostnomicClientOptions> options) =>
{
    var blogInfo = await blog.GetBlogAsync();
    var xml = await PostnomicFeedBuilder.BuildRssAsync(
        blog, "https://www.example.com", options.Value.BasePath, options.Value.LanguageRouteStyle,
        channelTitle: blogInfo?.Name ?? "Blog", channelDescription: blogInfo?.Description);
    return Results.Content(xml, "application/rss+xml");
});
```

Swap in your app's real public origin for the `absoluteBaseUrl` argument (don't hardcode
`https://www.example.com`) -- both methods emit the same XML shape as
`Postnomic.Client.AspNetCore`'s `MapPostnomicBlog()`, so a host migrating between the two hosting
models gets byte-identical feeds.

## Components

| Component | Purpose |
|---|---|
| `BlogPage` | Post list with pagination, tag/category/search filters, sidebar widgets |
| `PostPage` | Full post detail, comments, comment submission form |
| `AuthorPage` | Author profile page |
| `PostnomicBlogScope` | Cascades a named blog's `IPostnomicBlogService`/options to child components -- wrap your pages in it when using multi-blog named registrations |
| `Sidebar/*` (`TagCloud`, `CategoryList`, `AuthorList`, `SearchBox`, `MostReadPosts`, `TopCommentedPosts`, `EstimatedReadTime`, `PostnomicPromo`) | Individual sidebar widgets, usable standalone |

## Multi-Blog Support

Call `AddPostnomicBlog(name, configure)` once per blog, then wrap the corresponding pages in
`<PostnomicBlogScope BlogName="name">` so child components resolve the right blog:

```razor
@page "/blog/{Tier}/post/{PostSlug}"
@using Postnomic.Client.Blazor.Components
@using Postnomic.Client.Blazor.Components.Pages

<PostnomicBlogScope BlogName="@Tier">
    <PostPage PostSlug="@PostSlug" />
</PostnomicBlogScope>

@code {
    [Parameter] public string Tier { get; set; } = "";
    [Parameter] public string PostSlug { get; set; } = "";
}
```

## Requirements

- .NET 10.0 or later
- A `<HeadOutlet />` in your app's root component (already present in the standard Blazor Web App
  template)

## Links

- [Root SDK README](../../README.md) -- full options reference, multi-language posts, and more
- [Postnomic Website](https://www.postnomic.com)
- [Report an Issue](https://github.com/threeb-it/postnomic-dotnet/issues)
