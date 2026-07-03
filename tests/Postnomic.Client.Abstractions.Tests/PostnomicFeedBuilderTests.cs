using FluentAssertions;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Abstractions.Seo;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

public class PostnomicFeedBuilderTests
{
    private static Mock<IPostnomicBlogService> Svc()
    {
        var m = new Mock<IPostnomicBlogService>();
        m.Setup(s => s.GetPostsAsync(It.IsAny<int>(), It.IsAny<int>(), null, null, null, null, null, default))
         .ReturnsAsync(new PostnomicPagedResult<PostnomicPostSummary>
         {
             Items = [ new PostnomicPostSummary { Slug="hello", Title="Hello", Language="en",
                 AuthorName="Jane Doe",
                 AvailableLanguages=["en"], PublishedAt=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc) } ],
             Page=1, PageSize=50, TotalCount=1, TotalPages=1
         });
        return m;
    }

    [Fact]
    public async Task Sitemap_uses_absolute_base_url()
    {
        var xml = await PostnomicFeedBuilder.BuildSitemapAsync(
            Svc().Object, "https://www.outastory.com", "/blog", PostnomicLanguageRouteStyle.None);
        xml.Should().Contain("https://www.outastory.com/blog/post/hello").And.NotContain("file:");
    }

    [Fact]
    public async Task Rss_uses_absolute_base_url_and_channel_title()
    {
        var xml = await PostnomicFeedBuilder.BuildRssAsync(
            Svc().Object, "https://www.outastory.com", "/blog", PostnomicLanguageRouteStyle.None,
            "OutaStory | Blog", "desc");
        xml.Should().Contain("<title>OutaStory | Blog</title>").And.Contain("https://www.outastory.com/blog/post/hello");
    }

    // Consumers serve the returned string as UTF-8 (e.g. Results.Content(xml, "application/rss+xml",
    // Encoding.UTF8)), so the declared prolog encoding must say utf-8, not the in-memory
    // string/StringBuilder's native utf-16 - a contradiction that strict XML/RSS validators reject.
    [Fact]
    public async Task Rss_declares_utf8_encoding_in_xml_prolog()
    {
        var xml = await PostnomicFeedBuilder.BuildRssAsync(
            Svc().Object, "https://www.outastory.com", "/blog", PostnomicLanguageRouteStyle.None,
            "OutaStory | Blog", "desc");

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.ToLowerInvariant().Should().NotContain("encoding=\"utf-16\"");
    }

    [Fact]
    public async Task Sitemap_declares_utf8_encoding_in_xml_prolog()
    {
        var xml = await PostnomicFeedBuilder.BuildSitemapAsync(
            Svc().Object, "https://www.outastory.com", "/blog", PostnomicLanguageRouteStyle.None);

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.ToLowerInvariant().Should().NotContain("encoding=\"utf-16\"");
    }
}
