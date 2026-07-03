using FluentAssertions;
using Postnomic.Client.Abstractions;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

public class PostnomicCssClassesTests
{
    [Fact]
    public void Bootstrap_mode_returns_legacy_classes()
    {
        var c = new PostnomicCssClasses(PostnomicMarkupStyle.Bootstrap);
        c.Card.Should().Be("card mb-4 shadow-sm");
        c.BtnOutline.Should().Be("btn btn-outline-primary btn-sm");
        c.Layout.Should().Be("row");
    }

    [Fact]
    public void Semantic_mode_returns_pn_classes()
    {
        var c = new PostnomicCssClasses(PostnomicMarkupStyle.Semantic);
        c.Card.Should().Be("pn-card");
        c.BtnOutline.Should().Be("pn-btn pn-btn--outline pn-btn--sm");
        c.Layout.Should().Be("pn-layout");
    }

    [Fact]
    public void Options_default_markup_style_is_bootstrap() =>
        new PostnomicClientOptions { BaseUrl = "x", ApiKey = "k", BlogSlug = "b" }
            .MarkupStyle.Should().Be(PostnomicMarkupStyle.Bootstrap);
}
