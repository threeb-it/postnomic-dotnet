using FluentAssertions;
using Postnomic.Client.Abstractions;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="PostnomicIconSvg"/> — the framework-free Bootstrap-icon-class-to-SVG
/// glyph map relocated here (from <c>Postnomic.Client.Blazor.PostnomicIcons</c>, Task 4) so both
/// the Blazor components and the AspNetCore Razor Pages Blog Area can render the exact same
/// distinct Semantic-mode icons without either host depending on the other's framework.
/// </summary>
public class PostnomicIconSvgTests
{
    [Fact]
    public void For_known_class_returns_svg_markup()
    {
        var svg = PostnomicIconSvg.For("bi bi-calendar");
        svg.Should().StartWith("<svg").And.EndWith("</svg>");
    }

    [Fact]
    public void For_different_known_classes_returns_distinct_glyphs()
    {
        var person = PostnomicIconSvg.For("bi bi-person");
        var calendar = PostnomicIconSvg.For("bi bi-calendar");
        person.Should().NotBe(calendar);
    }

    [Fact]
    public void For_unknown_class_returns_generic_fallback_glyph()
    {
        var svg = PostnomicIconSvg.For("bi bi-does-not-exist");
        svg.Should().Contain("<circle cx=\"8\" cy=\"8\" r=\"6\" />");
    }

    [Fact]
    public void For_same_class_is_deterministic()
    {
        PostnomicIconSvg.For("bi bi-search").Should().Be(PostnomicIconSvg.For("bi bi-search"));
    }
}
