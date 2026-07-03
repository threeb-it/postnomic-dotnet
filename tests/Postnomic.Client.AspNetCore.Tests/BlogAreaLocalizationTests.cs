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
}
