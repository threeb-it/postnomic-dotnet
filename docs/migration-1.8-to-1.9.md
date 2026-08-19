# Migrating 1.8 → 1.9

**Nothing breaks on upgrade.** One member is obsolete and one is new. You can upgrade, see the
build warning, and migrate later.

Related: [Per-post hreflang alternates](./hreflang-alternates.md) ·
[Client options reference](./client-options.md) · [Troubleshooting](./troubleshooting.md)

---

## What is obsolete

| Obsolete | Replacement |
|---|---|
| `PostnomicClientOptions.AlternateUrlResolver` | `IPostnomicAlternateUrlProvider`, registered with `services.AddPostnomicAlternateUrlProvider<TProvider>()` |

Setting `AlternateUrlResolver` now raises `CS0618`. It still works and is still honoured at render
time; it will be removed in a future **major** version. If a provider is also registered, the
provider wins.

## What is new

- `IPostnomicAlternateUrlProvider` (`Postnomic.Client.Abstractions`) — the replacement seam.
- `ServiceCollectionExtensions.AddPostnomicAlternateUrlProvider<TProvider>(...)`
  (`Postnomic.Client`), with an overload taking a blog `name` for keyed, per-blog registration in
  multi-blog hosts.
- `PostnomicAlternateUrls.ResolveAsync(...)` (`Postnomic.Client`) — the shared resolution helper both
  hosting models use, public so a host rendering its own SEO head can apply identical precedence.

## The replacement is async

That is the point of the change. `AlternateUrlResolver` was a synchronous `Func`, so it could not
make the API call needed to discover a translation's real slug — a translated slug is **not**
derivable from the original's. Consumers worked around this by splitting their resolver into an
async cache-warming half and a sync cache-reading half and relying on render ordering.

`GetAlternatesAsync` returns a `ValueTask`, so the lookup happens inline, in order, with no
cache-warming and no sync-over-async.

## Before / after

**Before** — synchronous, and unusable with `Configure<TDep>` if the dependency touches the SDK
(see [Client options reference](./client-options.md)):

```csharp
builder.Services.AddPostnomicBlog(options =>
{
    options.BaseUrl  = "https://api.postnomic.com";
    options.BlogSlug = "my-blog";
    options.AlternateUrlResolver = post => _slugCache.TryGetValue(post.Slug, out var urls)
        ? urls          // populated earlier, out of band, by an async warm-up pass
        : null;
});
```

**After** — one class, one registration, no cache to warm:

```csharp
public sealed class BlogAlternateUrlProvider(IPostnomicBlogService blog)
    : IPostnomicAlternateUrlProvider
{
    public async ValueTask<IReadOnlyList<(string Language, string Url)>?> GetAlternatesAsync(
        PostnomicPostDetail post,
        CancellationToken cancellationToken = default)
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

builder.Services.AddPostnomicBlog(options =>
{
    options.BaseUrl  = "https://api.postnomic.com";
    options.BlogSlug = "my-blog";
});

builder.Services.AddPostnomicAlternateUrlProvider<BlogAlternateUrlProvider>();
```

Ordering and de-duplication behaviour are unchanged: the first entry is the `x-default` target, and
two languages resolving to one URL still collapse to a single `hreflang` entry.

## Suppressing the warning without migrating

If you are deliberately staying on the obsolete hook for now:

```csharp
#pragma warning disable CS0618
options.AlternateUrlResolver = post => /* ... */;
#pragma warning restore CS0618
```

## Also in this release

`PostPage.AlternateUrls` (Blazor) is no longer a computed property reading the options callback; it
is resolved once, asynchronously, after the post loads. It was `private` in 1.8 and remains
non-public — the supported seam is the provider, not a component parameter. See
[the guide](./hreflang-alternates.md#supplying-alternates-the-supported-way).

`PostModel.AlternateUrls` (ASP.NET Core) stays public but is now `{ get; private set; }`, populated
during page load. It is page **output** for the SEO head, not an input seam.
