using Postnomic.Client.Abstractions;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

public class PostnomicRouteBuilderTests
{
    [Theory]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, "de", "/blog/de/post/hello")]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "de", "/de/blog/post/hello")]
    [InlineData(PostnomicLanguageRouteStyle.None,   "de", "/blog/post/hello")]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, null, "/blog/post/hello")]
    public void BuildPost_shapes(PostnomicLanguageRouteStyle style, string? lang, string expected)
        => Assert.Equal(expected, PostnomicRouteBuilder.BuildPost("/blog", style, lang, "hello"));

    [Theory]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/de/blog/post/x", "de")]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/blog/post/x", null)]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, "/blog/de/post/x", "de")]
    public void ExtractLang_works(PostnomicLanguageRouteStyle style, string path, string? expected)
        => Assert.Equal(expected, PostnomicRouteBuilder.ExtractLang(path, "/blog", style));

    [Theory]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/de/blog", true)]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/de/blog/post/x", true)]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/other", false)]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, "/blog/de", true)]
    public void MatchesBlog_works(PostnomicLanguageRouteStyle style, string path, bool expected)
        => Assert.Equal(expected, PostnomicRouteBuilder.MatchesBlog(path, "/blog", style));
}
