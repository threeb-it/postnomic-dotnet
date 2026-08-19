# Per-post hreflang alternates

How to tell the SDK a post's **real** per-language URLs, so `<link rel="alternate" hreflang="...">`,
`x-default`, and the sitemap point at pages that actually exist.

- [Why the SDK cannot work this out itself](#why-the-sdk-cannot-work-this-out-itself)
- [The three slug shapes](#the-three-slug-shapes)
- [Never compose a translated slug](#never-compose-a-translated-slug)
- [Supplying alternates: the supported way](#supplying-alternates-the-supported-way)
- [Multi-blog hosts](#multi-blog-hosts)
- [Precedence and fall-through](#precedence-and-fall-through)
- [Duplicate URLs are collapsed](#duplicate-urls-are-collapsed)

Related: [Client options reference](./client-options.md) ·
[Migration 1.8 → 1.9](./migration-1.8-to-1.9.md) · [Troubleshooting](./troubleshooting.md)

---

## Why the SDK cannot work this out itself

`PostnomicPostDetail` carries `Language` and `AvailableLanguages`, but **no per-language slug
field** — and neither does the authoring-side translation model. So when the SDK composes
alternates itself (`PostnomicRouteBuilder.BuildPostAlternates`), all it can do is apply one
`PostnomicLanguageRouteStyle` to the *same* slug for every language.

That is correct only when every translation of a post really does share the original's slug. Under
`PostnomicLanguageRouteStyle.None` it is never correct for more than one language, because no
language gets its own URL segment at all — every composed alternate collapses onto the identical
bare URL.

If your translations do not all share one slug, the host application is the only party that knows
the real URLs, so the host has to supply them.

## The three slug shapes

These are the shapes observed on live Postnomic blogs. A single blog can contain all three.

| Shape | German post | English translation | What the SDK can compose |
|---|---|---|---|
| **Identical slug** — one URL serving both languages, content-negotiated | `/blog/post/geteilter-artikel` | `/blog/post/geteilter-artikel` | Correct, but only one honest hreflang entry exists — see [Duplicate URLs are collapsed](#duplicate-urls-are-collapsed) |
| **Suffixed slug** — a language suffix appended to the original | `/blog/post/kurze-hoerbuecher` | `/blog/post/kurze-hoerbuecher-en` | Correct **only** under `Suffix`/`Prefix` styles, and only if the suffix convention matches exactly |
| **Fully translated slug** — the slug itself is translated | `/blog/post/kurze-hoerbuecher` | `/blog/post/short-audiobooks` | **Impossible** — nothing in the API response relates the two slugs |

The third row is why the resolver seam exists. There is no rule that derives `short-audiobooks`
from `kurze-hoerbuecher`.

## Never compose a translated slug

Do **not** build an alternate by string-concatenating a language onto the original slug:

```csharp
// WRONG. Produces a URL that does not exist whenever the translation has its own slug.
var alternate = $"/blog/post/{post.Slug}-en";
```

On one production blog this exact pattern put **27 hard 404s into a live sitemap** — every post
whose English translation had a genuinely translated slug got a fabricated `-en` URL that returned
404 to the crawler. Search engines treat a 404 in a submitted sitemap as a quality signal against
the whole file, and an `hreflang` pointing at a 404 is dropped rather than followed.

Look the real slug up instead — see below.

## Supplying alternates: the supported way

Implement [`IPostnomicAlternateUrlProvider`](../src/Postnomic.Client.Abstractions/IPostnomicAlternateUrlProvider.cs)
and register it. The SDK resolves it **from dependency injection at the point of render**, so your
implementation may freely depend on `IPostnomicBlogService` — or anything else that touches the SDK.

```csharp
using Postnomic.Client;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

public sealed class BlogAlternateUrlProvider(IPostnomicBlogService blog)
    : IPostnomicAlternateUrlProvider
{
    public async ValueTask<IReadOnlyList<(string Language, string Url)>?> GetAlternatesAsync(
        PostnomicPostDetail post,
        CancellationToken cancellationToken = default)
    {
        var alternates = new List<(string Language, string Url)>();

        // Put the blog's default language FIRST: the first entry becomes hreflang="x-default".
        foreach (var language in post.AvailableLanguages)
        {
            // Ask the API for the translation so its REAL slug is used.
            var translated = await blog.GetPostAsync(post.Slug, language, cancellationToken);
            if (translated is not null)
                alternates.Add((language, $"/blog/post/{translated.Slug}"));
        }

        return alternates.Count > 0 ? alternates : null;
    }
}
```

Register it alongside the blog:

```csharp
builder.Services.AddPostnomicBlog(options =>
{
    options.BaseUrl  = "https://api.postnomic.com";
    options.ApiKey   = "pk_...";
    options.BlogSlug = "my-blog";
});

builder.Services.AddPostnomicAlternateUrlProvider<BlogAlternateUrlProvider>();
```

That is the whole wiring. It works identically for `Postnomic.Client.AspNetCore` (Razor Pages) and
`Postnomic.Client.Blazor` — both hosting models resolve the provider the same way and emit identical
SEO output.

`AddPostnomicAlternateUrlProvider<T>()` registers the provider as **`Scoped`** by default, which
suits a provider that caches per request or per Blazor circuit. Pass a `ServiceLifetime` to change
it:

```csharp
builder.Services.AddPostnomicAlternateUrlProvider<BlogAlternateUrlProvider>(ServiceLifetime.Singleton);
```

> **Do not** configure this through `PostnomicClientOptions` with the DI-aware
> `OptionsBuilder.Configure<TDep>` overload. Every service this SDK registers consumes
> `IOptions<PostnomicClientOptions>`, so a dependency that touches the SDK makes options
> construction self-recurse and throw
> `ValueFactory attempted to access the Value property of this instance.`
> See [Client options reference](./client-options.md) and [Troubleshooting](./troubleshooting.md).

### Returning URLs

Each URL may be root-relative (`/blog/post/short-audiobooks`) or absolute
(`https://example.com/blog/post/short-audiobooks`). Both are normalised the same way the SDK's own
composed alternates are.

Return `null` for a post to fall back to the SDK's composed alternates for **that post
specifically** — useful when only some posts have translated slugs.

## Multi-blog hosts

A named blog can have its own provider, registered as a keyed service — mirroring how a named blog's
`IPostnomicBlogService` is registered:

```csharp
builder.Services.AddPostnomicBlog("marketing", options => { /* ... */ });
builder.Services.AddPostnomicBlog("engineering", options => { /* ... */ });

builder.Services.AddPostnomicAlternateUrlProvider<MarketingAlternateUrlProvider>("marketing");
builder.Services.AddPostnomicAlternateUrlProvider<SharedAlternateUrlProvider>();  // fallback
```

A blog with no provider of its own falls back to the unkeyed provider, if one is registered.

## Precedence and fall-through

For each rendered post the SDK takes the first of:

1. an `IPostnomicAlternateUrlProvider` registered for this blog's name (keyed);
2. an unkeyed `IPostnomicAlternateUrlProvider`;
3. the obsolete `PostnomicClientOptions.AlternateUrlResolver`, still honoured — see
   [Migration 1.8 → 1.9](./migration-1.8-to-1.9.md);
4. otherwise the SDK's composed alternates.

A registered provider that returns `null` for a post falls through to the **composed alternates**,
not to the obsolete resolver — so there is exactly one source of truth per post.

## Duplicate URLs are collapsed

When two or more languages genuinely resolve to the same URL, only the **first** entry for that URL
is kept; the rest are dropped, whether they came from a provider or from the composed fallback.

Two `hreflang` values pointing at one URL does not describe a language split. Google cannot infer
one from a single crawled URL no matter how many `hreflang` links claim it, so the SDK emits the
honest single entry rather than a false multi-URL cluster. Keeping the first occurrence also keeps
`PostnomicSeoModel.XDefaultUrl` coherent with its first-entry contract.

This is why the *identical slug* row in [the table above](#the-three-slug-shapes) yields one
alternate, not two.
