using FluentAssertions;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

public class DefaultStylesheetTests
{
    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Postnomic.Client.slnx"))) d = d.Parent;
        d.Should().NotBeNull(); return d!.FullName;
    }

    [Fact]
    public void Shared_stylesheet_exists_and_defines_contract()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "assets", "postnomic-blog.css"));
        foreach (var token in new[] { "--pn-surface", "--pn-primary", "--pn-radius", ".pn-blog", ".pn-card", ".pn-btn", ".pn-post-content" })
            css.Should().Contain(token);
        css.Should().NotContain(".col-lg");
    }

    [Theory]
    [InlineData("src/Postnomic.Client.Blazor/Postnomic.Client.Blazor.csproj")]
    [InlineData("src/Postnomic.Client.AspNetCore/Postnomic.Client.AspNetCore.csproj")]
    public void Both_packages_ship_the_stylesheet(string csprojRelative)
    {
        var proj = File.ReadAllText(Path.Combine(RepoRoot(), csprojRelative));
        proj.Should().Contain("postnomic-blog.css");
    }
}
