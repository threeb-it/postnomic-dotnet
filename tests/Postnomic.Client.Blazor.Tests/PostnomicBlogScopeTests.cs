using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Blazor.Components;

namespace Postnomic.Client.Blazor.Tests;

/// <summary>
/// bUnit tests for <see cref="PostnomicBlogScope"/>. Verifies it renders scoped child content when
/// the named blog is registered, and — the regression for GitHub #162 / POSTNOMIC-BLAZORDEMO-2 —
/// throws an actionable error (naming the blog and AddPostnomicBlog) instead of the opaque
/// "No keyed service for type 'IPostnomicBlogService' ... has been registered" framework crash.
/// </summary>
public class PostnomicBlogScopeTests : BunitContext
{
    public PostnomicBlogScopeTests()
    {
        Services.AddLogging();
    }

    [Fact]
    public void RendersChildContent_WhenBlogIsRegistered()
    {
        var blogService = new Mock<IPostnomicBlogService>().Object;
        Services.AddKeyedSingleton<IPostnomicBlogService>("krause-engineering", blogService);

        var monitor = new Mock<IOptionsMonitor<PostnomicClientOptions>>();
        monitor.Setup(m => m.Get(It.IsAny<string>())).Returns(new PostnomicClientOptions());
        Services.AddSingleton(monitor.Object);

        var cut = Render<PostnomicBlogScope>(p => p
            .Add(s => s.BlogName, "krause-engineering")
            .AddChildContent("<span>scoped-content</span>"));

        Assert.Contains("scoped-content", cut.Markup);
    }

    [Fact]
    public void ThrowsActionableError_WhenBlogNameNotRegistered()
    {
        // No keyed IPostnomicBlogService registered for this name — reproduces #162.
        var act = () => Render<PostnomicBlogScope>(p => p
            .Add(s => s.BlogName, "unregistered-blog"));

        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("unregistered-blog", ex.Message);
        Assert.Contains("AddPostnomicBlog", ex.Message);
    }
}
