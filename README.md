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

// That's it -- your blog is live at /blog
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

    // Optional: enable client-side caching
    options.Cache = new PostnomicCacheOptions
    {
        Enabled = true,
        PostListDuration = TimeSpan.FromMinutes(5),
        PostDetailDuration = TimeSpan.FromMinutes(10),
    };
});
```

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
- **Client-Side Caching** -- optional in-memory cache with per-resource TTLs and explicit invalidation via `IPostnomicCacheControl`

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

### AspNetCore routes

`Postnomic.Client.AspNetCore` adds `/{lang}/` variants of the blog's routes alongside the canonical (default-language) ones, where `{lang}` is constrained to exactly two lowercase letters:

- `/{basePath}` and `/{basePath}/{lang}` -- index
- `/{basePath}/post/{postSlug}` and `/{basePath}/{lang}/post/{postSlug}` -- post detail
- `/{basePath}/author/{authorSlug}` and `/{basePath}/{lang}/author/{authorSlug}` -- author page

### Blazor components

`PostPage` and `BlogPage` (`Postnomic.Client.Blazor`) both accept an optional `Language` parameter, which you can bind to your own routed `{lang}` segment.

### SEO / hreflang

The `Postnomic.Client.AspNetCore` post page model (`PostModel`) exposes `CanonicalUrl` and a list of `AlternateLanguageUrls` (`(string Language, string Url)` pairs). The SDK renders the post content but doesn't inject into your `<head>` -- render these yourself in your layout's `<head>`:

```cshtml
<link rel="canonical" href="@Model.CanonicalUrl" />
@foreach (var (language, url) in Model.AlternateLanguageUrls)
{
    <link rel="alternate" hreflang="@language" href="@url" />
}
```

`CanonicalUrl` and `AlternateLanguageUrls` are only defined on the post detail page model (`PostModel`) -- `Index`/`Author` don't expose them.

## Requirements

- .NET 10.0 or later
- A Postnomic account ([sign up free](https://www.postnomic.com))
- An API key from your Postnomic dashboard

## Project Structure

```
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

---

Built with care by [ThreeB IT GmbH](https://www.threebit.io) in Ibbenbueren, Germany.
