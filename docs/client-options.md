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
| `AlternateUrlResolver` | `Func<...>?` | `null` | **Obsolete.** See below. |

### `Cache` (`PostnomicCacheOptions`)

| Option | Type | Default |
|---|---|---|
| `Enabled` | `bool` | `false` |
| `MetadataDuration` | `TimeSpan` | 5 min |
| `PostListDuration` | `TimeSpan` | 2 min |
| `PostDetailDuration` | `TimeSpan` | 5 min |
| `PopularPostsDuration` | `TimeSpan` | 10 min |

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
