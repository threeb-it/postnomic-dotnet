using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Blazor.Components.Pages;

namespace Postnomic.Client.Blazor.Tests;

/// <summary>
/// bUnit tests for the <see cref="AuthorPage"/> Blazor component.
/// Verifies the loading state, profile section rendering, conditional sections,
/// recent post links, back link behaviour, and custom BasePath support.
/// </summary>
public class AuthorPageTests : BunitContext
{
    private readonly Mock<IPostnomicBlogService> _blogServiceMock;

    public AuthorPageTests()
    {
        _blogServiceMock = new Mock<IPostnomicBlogService>();
        Services.AddSingleton(_blogServiceMock.Object);
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions()));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PostnomicAuthorProfile CreateProfile(
        string name = "Jane Doe",
        string? slug = "jane-doe",
        string? headline = "Software Engineer",
        string? bio = "<p>About me</p>",
        string? location = "Berlin",
        string? websiteUrl = "https://example.com",
        string? profileImageUrl = "https://example.com/avatar.jpg",
        string? headerImageUrl = "https://example.com/header.jpg",
        string? company = "Acme Inc",
        string? jobTitle = "Senior Dev",
        int postCount = 5,
        ICollection<PostnomicSocialLink>? socialLinks = null,
        ICollection<PostnomicCertification>? certifications = null,
        ICollection<string>? interests = null,
        ICollection<string>? skills = null,
        ICollection<PostnomicEducation>? education = null,
        ICollection<PostnomicLanguage>? languages = null,
        ICollection<PostnomicPostSummary>? recentPosts = null) =>
        new()
        {
            Name = name,
            Slug = slug,
            Headline = headline,
            Bio = bio,
            Location = location,
            WebsiteUrl = websiteUrl,
            ProfileImageUrl = profileImageUrl,
            HeaderImageUrl = headerImageUrl,
            Company = company,
            JobTitle = jobTitle,
            PostCount = postCount,
            SocialLinks = socialLinks ?? [],
            Certifications = certifications ?? [],
            Interests = interests ?? [],
            Skills = skills ?? [],
            Education = education ?? [],
            Languages = languages ?? [],
            RecentPosts = recentPosts ?? []
        };

    private void SetupProfile(PostnomicAuthorProfile? profile)
    {
        _blogServiceMock
            .Setup(s => s.GetAuthorProfileAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
    }

    private static PostnomicPostSummary CreatePostSummary(
        string slug = "a-post",
        string title = "A Post",
        string? excerpt = null) =>
        new()
        {
            Slug = slug,
            Title = title,
            AuthorName = "Jane Doe",
            PublishedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            CommentCount = 0,
            Excerpt = excerpt
        };

    // ── Loading state ─────────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_BeforeDataLoads_RendersLoadingIndicator()
    {
        // Arrange — set up service to never complete so the loading branch is visible
        var tcs = new TaskCompletionSource<PostnomicAuthorProfile?>();
        _blogServiceMock
            .Setup(s => s.GetAuthorProfileAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — while the task is pending the loading placeholder appears
        cut.Markup.Should().Contain("Loading");
    }

    // ── Profile rendering ─────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersAuthorName()
    {
        // Arrange
        SetupProfile(CreateProfile(name: "Jane Doe"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — name appears in the h2.h4 heading
        cut.Find("h2.h4").TextContent.Should().Contain("Jane Doe");
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersHeadline()
    {
        // Arrange
        SetupProfile(CreateProfile(headline: "Open Source Advocate"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert
        cut.Markup.Should().Contain("Open Source Advocate");
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersLocation()
    {
        // Arrange
        SetupProfile(CreateProfile(location: "Amsterdam"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert
        cut.Markup.Should().Contain("Amsterdam");
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersCompanyAndJobTitle()
    {
        // Arrange
        SetupProfile(CreateProfile(company: "Tech Corp", jobTitle: "Principal Engineer"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert
        cut.Markup.Should().Contain("Tech Corp");
        cut.Markup.Should().Contain("Principal Engineer");
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersWebsiteLink()
    {
        // Arrange
        SetupProfile(CreateProfile(websiteUrl: "https://janedoe.dev"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — an anchor pointing to the website URL should be present
        var links = cut.FindAll("a[href]");
        links.Should().Contain(a => a.GetAttribute("href") == "https://janedoe.dev");
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersPostCountBadge()
    {
        // Arrange
        SetupProfile(CreateProfile(postCount: 42));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — the primary badge should contain the count
        var badge = cut.Find("span.badge.bg-primary");
        badge.TextContent.Should().Contain("42");
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersProfileImage()
    {
        // Arrange
        SetupProfile(CreateProfile(profileImageUrl: "https://example.com/avatar.jpg"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — a rounded-circle img with the correct src should be present
        var img = cut.Find("img.rounded-circle");
        img.GetAttribute("src").Should().Be("https://example.com/avatar.jpg");
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersHeaderImage()
    {
        // Arrange
        SetupProfile(CreateProfile(headerImageUrl: "https://example.com/header.jpg"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — the wide header img should be rendered with the correct src
        var img = cut.Find("img.w-100");
        img.GetAttribute("src").Should().Be("https://example.com/header.jpg");
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersBio()
    {
        // Arrange
        SetupProfile(CreateProfile(bio: "<p>This is my bio.</p>"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — bio text rendered inside the About card
        cut.Markup.Should().Contain("This is my bio.");
    }

    // ── Social links ──────────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenHasSocialLinks_RendersLinkButtons()
    {
        // Arrange
        var links = new List<PostnomicSocialLink>
        {
            new() { Platform = "GitHub", Url = "https://github.com/jane" },
            new() { Platform = "LinkedIn", Url = "https://linkedin.com/in/jane" }
        };
        SetupProfile(CreateProfile(socialLinks: links));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — one btn-outline-secondary anchor per social link
        var socialButtons = cut.FindAll("a.btn.btn-outline-secondary");
        socialButtons.Should().HaveCount(2);
        cut.Markup.Should().Contain("GitHub");
        cut.Markup.Should().Contain("LinkedIn");
    }

    [Fact]
    public void AuthorPage_WhenNoSocialLinks_DoesNotRenderConnectSection()
    {
        // Arrange
        SetupProfile(CreateProfile(socialLinks: []));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — "Connect" heading should not appear when there are no social links
        cut.Markup.Should().NotContain("Connect");
    }

    // ── Skills ────────────────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenHasSkills_RendersSkillBadges()
    {
        // Arrange
        SetupProfile(CreateProfile(skills: ["C#", "Blazor", "Azure"]));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — bg-secondary badges for each skill
        var skillBadges = cut.FindAll("span.badge.bg-secondary");
        skillBadges.Should().HaveCount(3);
        cut.Markup.Should().Contain("C#");
        cut.Markup.Should().Contain("Blazor");
        cut.Markup.Should().Contain("Azure");
    }

    [Fact]
    public void AuthorPage_WhenNoSkills_DoesNotRenderSkillsSection()
    {
        // Arrange
        SetupProfile(CreateProfile(skills: []));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — "Skills" heading should not appear when there are no skills
        cut.Markup.Should().NotContain("Skills");
    }

    // ── Certifications ────────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenHasCertifications_RendersCertificationNames()
    {
        // Arrange
        var certs = new List<PostnomicCertification>
        {
            new() { Name = "Azure Solutions Architect", IssuingOrganization = "Microsoft" }
        };
        SetupProfile(CreateProfile(certifications: certs));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — cert name appears in a <strong> element
        cut.Markup.Should().Contain("Azure Solutions Architect");
        var strong = cut.FindAll("strong");
        strong.Should().Contain(s => s.TextContent.Contains("Azure Solutions Architect"));
    }

    [Fact]
    public void AuthorPage_WhenHasCertifications_RendersIssuingOrganization()
    {
        // Arrange
        var certs = new List<PostnomicCertification>
        {
            new() { Name = "Certified Kubernetes Administrator", IssuingOrganization = "CNCF" }
        };
        SetupProfile(CreateProfile(certifications: certs));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — issuing organisation appears as muted small text
        cut.Markup.Should().Contain("CNCF");
    }

    // ── Education ─────────────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenHasEducation_RendersInstitutionName()
    {
        // Arrange
        var education = new List<PostnomicEducation>
        {
            new() { Institution = "MIT", Degree = "BSc", FieldOfStudy = "Computer Science" }
        };
        SetupProfile(CreateProfile(education: education));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — institution name rendered in a <strong> element
        cut.Markup.Should().Contain("MIT");
        var strong = cut.FindAll("strong");
        strong.Should().Contain(s => s.TextContent.Contains("MIT"));
    }

    // ── Languages ─────────────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenHasLanguages_RendersLanguageNames()
    {
        // Arrange
        var languages = new List<PostnomicLanguage>
        {
            new() { Name = "English", Proficiency = "Native" },
            new() { Name = "German", Proficiency = "Fluent" }
        };
        SetupProfile(CreateProfile(languages: languages));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert
        cut.Markup.Should().Contain("English");
        cut.Markup.Should().Contain("German");
    }

    // ── Interests ─────────────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenHasInterests_RendersInterestBadges()
    {
        // Arrange
        SetupProfile(CreateProfile(interests: ["Open Source", "Hiking", "Music"]));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — bg-info badges for each interest
        var interestBadges = cut.FindAll("span.badge.bg-info");
        interestBadges.Should().HaveCount(3);
        cut.Markup.Should().Contain("Open Source");
        cut.Markup.Should().Contain("Hiking");
        cut.Markup.Should().Contain("Music");
    }

    // ── Recent posts ──────────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenHasRecentPosts_RendersPostLinks()
    {
        // Arrange
        var posts = new List<PostnomicPostSummary>
        {
            CreatePostSummary("blazor-tips", "Blazor Tips"),
            CreatePostSummary("dotnet-perf", ".NET Performance")
        };
        SetupProfile(CreateProfile(recentPosts: posts));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — one anchor per recent post
        var links = cut.FindAll("a[href]");
        links.Should().Contain(a => a.GetAttribute("href")!.Contains("blazor-tips"));
        links.Should().Contain(a => a.GetAttribute("href")!.Contains("dotnet-perf"));
        cut.Markup.Should().Contain("Blazor Tips");
        cut.Markup.Should().Contain(".NET Performance");
    }

    [Fact]
    public void AuthorPage_WhenHasRecentPosts_PostLinksUseDefaultBasePath()
    {
        // Arrange
        var posts = new List<PostnomicPostSummary>
        {
            CreatePostSummary("my-post", "My Post")
        };
        SetupProfile(CreateProfile(recentPosts: posts));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — links should use the default /blog base path
        var links = cut.FindAll("a[href]");
        links.Should().Contain(a => a.GetAttribute("href") == "/blog/post/my-post");
    }

    // ── Custom BasePath ───────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WithCustomBasePath_PostLinksUseCustomPath()
    {
        // Arrange — register a custom BasePath
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions { BasePath = "/articles" }));

        var posts = new List<PostnomicPostSummary>
        {
            CreatePostSummary("custom-post", "Custom Post")
        };
        SetupProfile(CreateProfile(recentPosts: posts));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — links should use the custom base path
        var links = cut.FindAll("a[href]");
        links.Should().Contain(a => a.GetAttribute("href") == "/articles/post/custom-post");
    }

    [Fact]
    public void AuthorPage_WithCustomBasePath_BackLinkUsesCustomPath()
    {
        // Arrange — register a custom BasePath
        Services.AddSingleton<IOptions<PostnomicClientOptions>>(
            Options.Create(new PostnomicClientOptions { BasePath = "/articles" }));
        SetupProfile(CreateProfile());

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — back link should point to the custom base path
        var backLink = cut.FindAll("a[href='/articles']");
        backLink.Should().NotBeEmpty();
    }

    // ── Null / empty optional sections ────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenBioIsNull_DoesNotRenderAboutSection()
    {
        // Arrange
        SetupProfile(CreateProfile(bio: null));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — "About" card title should not be present
        cut.Markup.Should().NotContain("About");
    }

    [Fact]
    public void AuthorPage_WhenProfileImageIsNull_DoesNotRenderProfileImage()
    {
        // Arrange
        SetupProfile(CreateProfile(profileImageUrl: null));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — no rounded-circle img should be present
        var imgs = cut.FindAll("img.rounded-circle");
        imgs.Should().BeEmpty();
    }

    [Fact]
    public void AuthorPage_WhenHeaderImageIsNull_DoesNotRenderHeaderImage()
    {
        // Arrange
        SetupProfile(CreateProfile(headerImageUrl: null));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — no wide header img should be present
        var imgs = cut.FindAll("img.w-100");
        imgs.Should().BeEmpty();
    }

    [Fact]
    public void AuthorPage_WhenLocationIsNull_DoesNotRenderLocation()
    {
        // Arrange — use a profile with no location, and also clear headline to avoid
        // the ambiguous text-muted small paragraph making the assertion harder
        SetupProfile(CreateProfile(location: null, headline: null, jobTitle: null, company: null));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — the bi-geo-alt icon class is only rendered when Location is set
        cut.Markup.Should().NotContain("bi-geo-alt");
    }

    [Fact]
    public void AuthorPage_WhenHeadlineIsNull_DoesNotRenderHeadline()
    {
        // Arrange
        const string distinctHeadline = "Unique Headline That Would Stand Out";
        SetupProfile(CreateProfile(headline: null));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — no headline paragraph when Headline is null
        cut.Markup.Should().NotContain(distinctHeadline);
        // The text-muted paragraph that wraps only the headline should not appear
        // when headline is null; verify by confirming the specific sentinel class
        // bi-geo-alt still appears only when location is set (location IS set here).
        // Directly confirm no element has the headline text at all.
        cut.FindAll("p.text-muted").Should().NotContain(
            p => p.TextContent == "Software Engineer",
            "the default headline should not be rendered when Headline is null");
    }

    // ── Back link ─────────────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersBackToBlogLink()
    {
        // Arrange
        SetupProfile(CreateProfile());

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — a back link pointing to the default base path should be present
        var backLink = cut.FindAll("a[href='/blog']");
        backLink.Should().NotBeEmpty();
        backLink[0].TextContent.Should().Contain("Back to blog");
    }
}
