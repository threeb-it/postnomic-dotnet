# `PostnomicClientOptions` reference

Configuration for every Postnomic client package. Bind it from configuration or set it inline in
`AddPostnomicClient` / `AddPostnomicBlog` / `AddPostnomicAuthoringClient`.

Related: [Per-post hreflang alternates](./hreflang-alternates.md) ·
[Migration 1.8 → 1.9](./migration-1.8-to-1.9.md) · [Troubleshooting](./troubleshooting.md)

---

## Every SDK service consumes these options

This is the single most important thing to know about configuring this SDK.

**Every service the SDK registers takes `IOptions<PostnomicClientOptions>`:**

| Service | Where |
|---|---|
| `PostnomicBlogService` | `src/Postnomic.Client/PostnomicBlogService.cs` |
| `CachingPostnomicBlogService` | `src/Postnomic.Client/CachingPostnomicBlogService.cs` |
| `PostnomicAuthoringService` | `src/Postnomic.Client/PostnomicAuthoringService.cs` |
| `PostnomicApiKeyHandler` | `src/Postnomic.Client/PostnomicApiKeyHandler.cs` |
| `PostnomicPersonalAccessTokenHandler` | `src/Postnomic.Client/PostnomicPersonalAccessTokenHandler.cs` |
| the typed `HttpClient` registrations behind them | `src/Postnomic.Client/PostnomicClientExtensions.cs` |

### The consequence

**No options callback may depend on a service that touches the SDK.**

Configuring options with the DI-aware `OptionsBuilder.Configure<TDep>` overload means building the
options *constructs `TDep` first*. If `TDep` needs `IPostnomicBlogService` — directly, or through
anything else — that service needs `IOptions<PostnomicClientOptions>`, which re-enters the
`Lazy<T>` currently being built. .NET detects the self-recursion and throws:

```text
System.InvalidOperationException: ValueFactory attempted to access the Value property of this instance.
```

The recursion closes inside `Microsoft.Extensions.Http.DefaultHttpClientFactory.CreateHandler`,
because the SDK's typed `HttpClient` registration reads
`IOptions<PostnomicClientOptions>.Value` to set `BaseAddress`.

Removing `IOptions<PostnomicClientOptions>` from your own constructor does **not** fix it. The edge
into the options object is *indirect*: any dependency reaching `IPostnomicBlogService`,
`IPostnomicAuthoringService`, or their handlers re-enters the same `Lazy<T>`. In practice that is
every realistic implementation, because looking up a translation's real slug requires an API call.

```csharp
// WRONG — throws at the first resolution of IOptions<PostnomicClientOptions>.
services.AddOptions<PostnomicClientOptions>()
    .Configure<MyResolver>((o, resolver) =>          // MyResolver -> IPostnomicBlogService
        o.AlternateUrlResolver = d => resolver.Lookup(d.Slug, d.Language));
```

```csharp
// RIGHT — a first-class service, resolved from DI at the point of render.
services.AddPostnomicAlternateUrlProvider<MyAlternateUrlProvider>();
```

Plain `Configure(Action<PostnomicClientOptions>)` — no `TDep` — is always safe, as is binding from
`IConfiguration`. The constraint is only on **DI-aware** options callbacks.

This behaviour is pinned by
`AlternateUrlProviderTests.ObsoleteWiring_ConfigureWithSdkTouchingDependency_ThrowsSelfRecursion`
in `tests/Postnomic.Client.Tests/AlternateUrlProviderTests.cs`, which asserts the exact message
quoted above.

## Options

| Option | Type | Default | Notes |
|---|---|---|---|
| `BaseUrl` | `string` | `""` | Postnomic API base URL, no trailing slash. |
| `ApiKey` | `string` | `""` | Sent as `X-Api-Key`. Read-only, scoped to one blog's published content. Used by `IPostnomicBlogService`; ignored by `IPostnomicAuthoringService`. |
| `BlogSlug` | `string` | `""` | Targets `IPostnomicBlogService`'s read routes. **Not** the same value as `BlogId`. |
| `PersonalAccessToken` | `string?` | `null` | `pnp_...`, sent as `Authorization: Bearer`. Required by `IPostnomicAuthoringService`; ignored by `IPostnomicBlogService`. |
| `BlogId` | `string?` | `null` | The blog's public GUID, required by the authoring routes. **Not** the same value as `BlogSlug`. |
| `BasePath` | `string` | `"/blog"` | Leading slash, no trailing slash. |
| `ShowBranding` | `bool` | `false` | Server-enforced on Free-tier blogs. |
| `Cache` | `PostnomicCacheOptions?` | `null` | `null` or `Enabled = false` means every call hits the API. |
| `LanguageRouteStyle` | `PostnomicLanguageRouteStyle` | `Suffix` | Where the language code appears in generated URLs. |
| `MarkupStyle` | `PostnomicMarkupStyle` | `Bootstrap` | `Semantic` opts into CSS-variable theming. |
| `UiStrings` | `PostnomicUiStringOverrides?` | `null` | Overrides the SDK's own chrome strings, not post content. |
| `FilterLinkRel` | `string?` | `null` | `rel` for filter, archive and pagination links. See below. |
| `AlternateUrlResolver` | `Func<...>?` | `null` | **Obsolete.** See below. |

### `Cache` (`PostnomicCacheOptions`)

| Option | Type | Default |
|---|---|---|
| `Enabled` | `bool` | `false` |
| `MetadataDuration` | `TimeSpan` | 5 min |
| `PostListDuration` | `TimeSpan` | 2 min |
| `PostDetailDuration` | `TimeSpan` | 5 min |
| `PopularPostsDuration` | `TimeSpan` | 10 min |

A `null` result (a post, author or blog that was not found) is cached for at most **one minute**, or the
configured duration if that is shorter, so a mistyped slug cannot pin a long-lived entry and a post published
right after a miss shows up quickly. Exceptions are never cached.

### `FilterLinkRel`

A `rel` value — typically `"nofollow"` — that the SDK's own views add to their **filter, archive and
pagination links**. Space-separated tokens are allowed (`"nofollow ugc"`).

```csharp
builder.Services.AddPostnomicBlog(options =>
{
    // ...
    options.FilterLinkRel = "nofollow";
});
```

```json
{ "Postnomic": { "FilterLinkRel": "nofollow" } }
```

**Why.** Every tag, category, author, search and page link is a distinct URL (`?tag=dotnet`,
`?p=3`, `?author=Jane`, …). A crawler follows all of them, and each one is a separate post-list
request to the API and a separate cache key — in the SDK's `CachingPostnomicBlogService` and in
the API's output cache — so crawl traffic multiplies the number of entries instead of hitting the
cache. Measured on one production blog: ~42 filter and paging variants per blog page and 86.5k
post-list requests a week before this was addressed. `nofollow` tells well-behaved crawlers not
to follow those links; the posts themselves stay fully linked and crawlable. It is a hint, not a
block: pair it with `robots.txt` rules if a crawler ignores it, and remember it does not remove
URLs that are already indexed (use `noindex` on the host page for that).

**Where it applies.**

| Link | Razor Pages (`Postnomic.Client.AspNetCore`) | Blazor (`Postnomic.Client.Blazor`) |
|---|---|---|
| Tag / category filter (`?tag=`, `?category=`) | Index (post cards + sidebar), Post | — (interactive buttons, no `href`) |
| Author filter (`?author=`) | Index sidebar | — (interactive button) |
| Author archive (`/author/{slug}`) | Index post cards, Post | `BlogPage`, `PostPage` |
| Search | the GET search `<form>` (`rel` is valid on `<form>`) | — (interactive button) |
| Pagination (`?p=`) | Index | — (interactive buttons) |

The Blazor tag cloud, category list, author list, search box and pager are `<button>`s with click
handlers — there is no URL for a crawler to follow, so there is nothing to mark.

Author archive pages (`/author/{slug}`) are real content pages, not query-string variants; they
are included because they are archive views of posts that are already linked directly. If you
want them crawled, leave the option unset and use `robots.txt` for the query-string variants
instead.

**Where it never applies.** Links to individual posts, the "back to blog" / "clear filter" links
to the plain index, the `<link rel="canonical">` / `<link rel="alternate">` tags, and external
links. An anchor that already carries a `rel` keeps it; the configured tokens are merged in
without duplicates.

**Default.** `null` (or whitespace) renders no `rel` attribute at all — the markup is unchanged.
Each named `AddPostnomicBlog(name, ...)` registration uses its own value.

**Related: the default page size is no longer in pagination URLs.** Independently of this option,
Razor Pages pagination links and the search form omit `PageSize` when it equals the default (5):
`/blog?p=2` instead of `/blog?p=2&PageSize=5`. The page binds the same default either way, so this
only removes a duplicate URL (and cache key) for the same page. A non-default `?PageSize=` is still
carried through.

### `AlternateUrlResolver` — obsolete

```csharp
[Obsolete] public Func<PostnomicPostDetail, IReadOnlyList<(string Language, string Url)>?>? AlternateUrlResolver
```

Superseded by [`IPostnomicAlternateUrlProvider`](./hreflang-alternates.md). Two reasons:

1. **It is synchronous**, so it cannot make the API call needed to discover a translation's real
   slug.
2. **It cannot be configured with a DI-aware callback** that touches the SDK, for the reason above —
   which is exactly what supplying real URLs requires.

Still honoured, so existing consumers keep working; a registered `IPostnomicAlternateUrlProvider`
takes precedence. See [Migration 1.8 → 1.9](./migration-1.8-to-1.9.md).
