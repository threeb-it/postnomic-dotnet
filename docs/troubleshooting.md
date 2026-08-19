# Troubleshooting

Symptoms first, keyed on the text you would paste into a search box.

Related: [Per-post hreflang alternates](./hreflang-alternates.md) ·
[Client options reference](./client-options.md) · [Migration 1.8 → 1.9](./migration-1.8-to-1.9.md)

---

## `ValueFactory attempted to access the Value property of this instance.`

```text
System.InvalidOperationException: ValueFactory attempted to access the Value property of this instance.
   at System.Lazy`1.CreateValue()
   at Microsoft.Extensions.Http.DefaultHttpClientFactory.CreateHandler(String name)
```

**Symptom.** Every blog route — and both sitemaps — returns HTTP 500 as soon as anything resolves
`IOptions<PostnomicClientOptions>`.

**Cause.** An options callback registered with the DI-aware `OptionsBuilder.Configure<TDep>`
overload depends on something that touches the SDK. Building the options constructs `TDep`, which
constructs an SDK service, which reads the options again. Every SDK service takes
`IOptions<PostnomicClientOptions>`, so the edge exists even when your own constructor does not
mention it — see [Client options reference](./client-options.md#every-sdk-service-consumes-these-options).

**Fix.** Move the dependency out of the options object and into a service the SDK resolves at the
point of use:

```csharp
// Delete this:
//   services.AddOptions<PostnomicClientOptions>()
//       .Configure<MyResolver>((o, r) => o.AlternateUrlResolver = d => r.Lookup(d.Slug, d.Language));

// Use this:
services.AddPostnomicAlternateUrlProvider<MyAlternateUrlProvider>();
```

Full walkthrough: [Supplying alternates](./hreflang-alternates.md#supplying-alternates-the-supported-way).

---

## `hreflang` URLs 404, or the sitemap contains dead links

**Symptom.** Alternates like `/blog/post/kurze-hoerbuecher-en` return 404. Search Console reports
sitemap errors.

**Cause.** A translated slug was **composed** by appending a language to the original slug. A
translation's slug is not derivable from the original's — it may be identical, suffixed, or fully
translated. This pattern put 27 hard 404s into one live production sitemap.

**Fix.** Look the real slug up through the API in an `IPostnomicAlternateUrlProvider`. See
[The three slug shapes](./hreflang-alternates.md#the-three-slug-shapes) and
[Never compose a translated slug](./hreflang-alternates.md#never-compose-a-translated-slug).

---

## Only one `hreflang` alternate is rendered when I returned two

**Symptom.** A provider (or the obsolete resolver) returned two languages, but the page shows one
`<link rel="alternate">` besides `x-default`.

**Cause.** Both entries resolved to the **same URL**. The SDK keeps the first and drops the rest:
two `hreflang` values on one URL does not describe a language split, and Google cannot infer one
from a single crawled URL.

**Fix.** Nothing, if the URLs really are identical — the single entry is the honest output. If they
should differ, the provider is returning composed rather than looked-up URLs. See
[Duplicate URLs are collapsed](./hreflang-alternates.md#duplicate-urls-are-collapsed).

---

## `CS0618: 'PostnomicClientOptions.AlternateUrlResolver' is obsolete`

**Symptom.** A build warning after upgrading to 1.9.

**Cause.** Expected. The hook is superseded by `IPostnomicAlternateUrlProvider` but still works.

**Fix.** Migrate when convenient — see [Migration 1.8 → 1.9](./migration-1.8-to-1.9.md) — or
suppress it locally with `#pragma warning disable CS0618`.

---

## My provider is never called

**Checklist.**

1. It is registered: `services.AddPostnomicAlternateUrlProvider<TProvider>()`.
2. In a **multi-blog** host, a keyed provider only serves the blog name it was registered under.
   A blog with no provider of its own falls back to an unkeyed one — register that fallback, or
   register per blog. See [Multi-blog hosts](./hreflang-alternates.md#multi-blog-hosts).
3. The provider is only consulted for **post detail** pages. Index and author pages have a single
   self-referential alternate by design.
4. If it returns `null`, the SDK falls back to composed alternates for that post — that is a
   deliberate opt-out, not a failure.
