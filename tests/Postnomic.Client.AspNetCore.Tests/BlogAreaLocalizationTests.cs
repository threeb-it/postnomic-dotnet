using System.Globalization;
using System.Resources;
using Xunit;

namespace Postnomic.Client.AspNetCore.Tests;

public sealed class BlogAreaLocalizationTests
{
    [Theory]
    [InlineData("de", "Weiterlesen")]
    [InlineData("en", "Read More")]
    public void IndexResx_ResolvesReadMore_PerCulture(string culture, string expected)
    {
        // The Index view's resources are embedded as
        // Postnomic.Client.AspNetCore.Areas.Blog.Pages.Index[.{culture}].resources
        var rm = new ResourceManager(
            "Postnomic.Client.AspNetCore.Areas.Blog.Pages.Index",
            typeof(PostnomicAspNetCoreExtensions).Assembly);

        var value = rm.GetString("ReadMore", CultureInfo.GetCultureInfo(culture));
        Assert.Equal(expected, value);
    }

    /// <summary>
    /// Fix 1 coverage: <c>Author.cshtml</c>'s resx (previously nonexistent -- the view had no
    /// resx-backed strings at all) resolves per culture just like Index's.
    /// </summary>
    [Theory]
    [InlineData("de", "Vernetzen")]
    [InlineData("en", "Connect")]
    public void AuthorResx_ResolvesConnect_PerCulture(string culture, string expected)
    {
        var rm = new ResourceManager(
            "Postnomic.Client.AspNetCore.Areas.Blog.Pages.Author",
            typeof(PostnomicAspNetCoreExtensions).Assembly);

        var value = rm.GetString("Connect", CultureInfo.GetCultureInfo(culture));
        Assert.Equal(expected, value);
    }
}
