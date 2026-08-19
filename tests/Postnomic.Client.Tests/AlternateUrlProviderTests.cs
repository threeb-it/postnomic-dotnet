using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Tests;

/// <summary>
/// A realistic host provider: it needs the SDK's own <see cref="IPostnomicBlogService"/> to look up
/// a translation's real slug, because <see cref="PostnomicPostDetail"/> carries no per-language
/// slug field. This is the shape that could not be expressed before
/// <see cref="IPostnomicAlternateUrlProvider"/> existed.
/// </summary>
internal sealed class BlogServiceBackedProvider(IPostnomicBlogService blogService)
    : IPostnomicAlternateUrlProvider
{
    public IPostnomicBlogService BlogService { get; } = blogService;

    public ValueTask<IReadOnlyList<(string Language, string Url)>?> GetAlternatesAsync(
        PostnomicPostDetail post,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<(string, string)> alternates =
        [
            ("de", $"/blog/post/{post.Slug}"),
            ("en", "/blog/post/short-audiobooks"),
        ];
        return ValueTask.FromResult<IReadOnlyList<(string Language, string Url)>?>(alternates);
    }
}

/// <summary>A provider that declines to answer, exercising the fall-through to composed alternates.</summary>
internal sealed class NullReturningProvider : IPostnomicAlternateUrlProvider
{
    public ValueTask<IReadOnlyList<(string Language, string Url)>?> GetAlternatesAsync(
        PostnomicPostDetail post,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<(string Language, string Url)>?>(null);
}

/// <summary>A second provider, used to prove keyed (per-named-blog) registration wins.</summary>
internal sealed class KeyedMarkerProvider : IPostnomicAlternateUrlProvider
{
    public ValueTask<IReadOnlyList<(string Language, string Url)>?> GetAlternatesAsync(
        PostnomicPostDetail post,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<(string, string)> alternates = [("de", "/keyed")];
        return ValueTask.FromResult<IReadOnlyList<(string Language, string Url)>?>(alternates);
    }
}

/// <summary>
/// A host dependency that touches the SDK, used to reproduce the options self-recursion that the
/// obsolete <see cref="PostnomicClientOptions.AlternateUrlResolver"/> wiring invites.
/// </summary>
internal sealed class BlogServiceBackedLookup(IPostnomicBlogService blogService)
{
    public IPostnomicBlogService BlogService { get; } = blogService;

    public IReadOnlyList<(string Language, string Url)>? Lookup(PostnomicPostDetail post) =>
        [("de", $"/blog/post/{post.Slug}")];
}

public class AlternateUrlProviderTests
{
    private static readonly PostnomicPostDetail Post = new()
    {
        Slug = "kurze-hoerbuecher",
        Title = "Kurze Hörbücher",
        AuthorName = "Autor",
        Language = "de",
        AvailableLanguages = ["de", "en"],
    };

    private static IServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddPostnomicClient(options =>
        {
            options.BaseUrl = "https://api.example.com";
            options.ApiKey = "test-key";
            options.BlogSlug = "test-blog";
        });
        return services;
    }

    private static PostnomicClientOptions OptionsOf(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value;

    // ── The defect: the obsolete options-callback wiring cannot use an SDK-touching dependency ──

    /// <summary>
    /// Pins the trap the documentation warns about. Binding the hreflang hook through the DI-aware
    /// <c>Configure&lt;TOptions, TDep&gt;</c> overload with a dependency that touches the SDK makes
    /// building the options construct that dependency, which constructs
    /// <see cref="IPostnomicBlogService"/>, whose typed <c>HttpClient</c> registration reads
    /// <c>IOptions&lt;PostnomicClientOptions&gt;.Value</c> again — re-entering the
    /// <see cref="Lazy{T}"/> currently being built.
    /// <para>
    /// This is inherent to that wiring and is NOT fixed by
    /// <see cref="IPostnomicAlternateUrlProvider"/>; the provider exists so a host never has to
    /// write this shape. The test exists so the exact message quoted in the troubleshooting docs
    /// stays accurate.
    /// </para>
    /// </summary>
    [Fact]
    public void ObsoleteWiring_ConfigureWithSdkTouchingDependency_ThrowsSelfRecursion()
    {
        var services = NewServices();
        services.AddSingleton<BlogServiceBackedLookup>();
        services.AddOptions<PostnomicClientOptions>()
            .Configure<BlogServiceBackedLookup>((options, lookup) =>
#pragma warning disable CS0618 // Deliberately exercising the obsolete wiring this test documents.
                options.AlternateUrlResolver = lookup.Lookup);
#pragma warning restore CS0618

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => OptionsOf(provider));
        Assert.Equal(
            "ValueFactory attempted to access the Value property of this instance.",
            exception.Message);
    }

    // ── The fix: a DI-resolved provider may depend on IPostnomicBlogService ────────────────────

    /// <summary>
    /// The acceptance case. A provider that depends on <see cref="IPostnomicBlogService"/> is
    /// registered, the options resolve without recursion — the operation that used to throw — and
    /// the provider itself resolves and answers asynchronously.
    /// </summary>
    [Fact]
    public async Task Provider_DependingOnBlogService_ResolvesOptionsAndAnswersAsync()
    {
        var services = NewServices();
        services.AddPostnomicAlternateUrlProvider<BlogServiceBackedProvider>();

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        var options = OptionsOf(scope.ServiceProvider);
        Assert.NotNull(options);

        var alternates = await PostnomicAlternateUrls.ResolveAsync(
            scope.ServiceProvider, options, blogName: null, Post);

        Assert.NotNull(alternates);
        Assert.Equal(
            [("de", "/blog/post/kurze-hoerbuecher"), ("en", "/blog/post/short-audiobooks")],
            alternates);
    }

    /// <summary>The provider really did receive the SDK service, not a stand-in.</summary>
    [Fact]
    public void Provider_DependingOnBlogService_ReceivesTheSdkBlogService()
    {
        var services = NewServices();
        services.AddPostnomicAlternateUrlProvider<BlogServiceBackedProvider>();

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        var resolved = Assert.IsType<BlogServiceBackedProvider>(
            scope.ServiceProvider.GetRequiredService<IPostnomicAlternateUrlProvider>());
        Assert.Same(
            scope.ServiceProvider.GetRequiredService<IPostnomicBlogService>(),
            resolved.BlogService);
    }

    // ── Precedence and fall-through ────────────────────────────────────────────────────────────

    /// <summary>The obsolete resolver keeps working for consumers who have not migrated.</summary>
    [Fact]
    public async Task ObsoleteResolver_WithNoProviderRegistered_IsStillHonoured()
    {
        var services = NewServices();
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        var options = OptionsOf(scope.ServiceProvider);
#pragma warning disable CS0618 // Proving the obsolete path still works is the point of this test.
        options.AlternateUrlResolver = post => [("de", $"/legacy/{post.Slug}")];
#pragma warning restore CS0618

        var alternates = await PostnomicAlternateUrls.ResolveAsync(
            scope.ServiceProvider, options, blogName: null, Post);

        Assert.Equal([("de", "/legacy/kurze-hoerbuecher")], alternates);
    }

    /// <summary>A registered provider wins over the obsolete resolver.</summary>
    [Fact]
    public async Task Provider_TakesPrecedenceOverTheObsoleteResolver()
    {
        var services = NewServices();
        services.AddPostnomicAlternateUrlProvider<BlogServiceBackedProvider>();

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        var options = OptionsOf(scope.ServiceProvider);
#pragma warning disable CS0618 // Asserting the provider wins over this deliberately-set legacy hook.
        options.AlternateUrlResolver = _ => [("de", "/legacy")];
#pragma warning restore CS0618

        var alternates = await PostnomicAlternateUrls.ResolveAsync(
            scope.ServiceProvider, options, blogName: null, Post);

        Assert.NotNull(alternates);
        Assert.Equal("/blog/post/kurze-hoerbuecher", alternates[0].Url);
    }

    /// <summary>
    /// A provider returning null falls through to the composed alternates (null), NOT to the
    /// obsolete resolver — one source of truth per post.
    /// </summary>
    [Fact]
    public async Task Provider_ReturningNull_DoesNotFallBackToTheObsoleteResolver()
    {
        var services = NewServices();
        services.AddPostnomicAlternateUrlProvider<NullReturningProvider>();

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        var options = OptionsOf(scope.ServiceProvider);
#pragma warning disable CS0618 // Must be ignored once a provider is registered.
        options.AlternateUrlResolver = _ => [("de", "/legacy")];
#pragma warning restore CS0618

        var alternates = await PostnomicAlternateUrls.ResolveAsync(
            scope.ServiceProvider, options, blogName: null, Post);

        Assert.Null(alternates);
    }

    /// <summary>With nothing registered at all, the SDK composes alternates as before.</summary>
    [Fact]
    public async Task NoProviderAndNoResolver_ReturnsNullSoComposedAlternatesAreUsed()
    {
        var services = NewServices();
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        var alternates = await PostnomicAlternateUrls.ResolveAsync(
            scope.ServiceProvider, OptionsOf(scope.ServiceProvider), blogName: null, Post);

        Assert.Null(alternates);
    }

    // ── Multi-blog: keyed registration ─────────────────────────────────────────────────────────

    /// <summary>A named blog's own provider wins over an unkeyed one.</summary>
    [Fact]
    public async Task KeyedProvider_ForNamedBlog_WinsOverTheUnkeyedProvider()
    {
        var services = NewServices();
        services.AddPostnomicAlternateUrlProvider<BlogServiceBackedProvider>();
        services.AddPostnomicAlternateUrlProvider<KeyedMarkerProvider>("second-blog");

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        var alternates = await PostnomicAlternateUrls.ResolveAsync(
            scope.ServiceProvider, OptionsOf(scope.ServiceProvider), "second-blog", Post);

        Assert.Equal([("de", "/keyed")], alternates);
    }

    /// <summary>A named blog with no provider of its own falls back to the unkeyed provider.</summary>
    [Fact]
    public async Task NamedBlogWithoutItsOwnProvider_FallsBackToTheUnkeyedProvider()
    {
        var services = NewServices();
        services.AddPostnomicAlternateUrlProvider<BlogServiceBackedProvider>();

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        var alternates = await PostnomicAlternateUrls.ResolveAsync(
            scope.ServiceProvider, OptionsOf(scope.ServiceProvider), "blog-with-no-provider", Post);

        Assert.NotNull(alternates);
        Assert.Equal("/blog/post/kurze-hoerbuecher", alternates[0].Url);
    }
}
