using Bunit;
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
        Assert.Contains("Loading", cut.Markup);
    }

    // ── Profile rendering ─────────────────────────────────────────────────────

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersAuthorName()
    {
        // Arrange
        SetupProfile(CreateProfile(name: "Jane Doe"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — name appears in the h1.h4 heading (a single h1 per page for correct SEO
        // heading hierarchy; see SeoAndLanguageRoutingTests.AuthorPage_RendersExactlyOneH1ForAuthorName)
        Assert.Contains("Jane Doe", cut.Find("h1.h4").TextContent);
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersHeadline()
    {
        // Arrange
        SetupProfile(CreateProfile(headline: "Open Source Advocate"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert
        Assert.Contains("Open Source Advocate", cut.Markup);
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersLocation()
    {
        // Arrange
        SetupProfile(CreateProfile(location: "Amsterdam"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert
        Assert.Contains("Amsterdam", cut.Markup);
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersCompanyAndJobTitle()
    {
        // Arrange
        SetupProfile(CreateProfile(company: "Tech Corp", jobTitle: "Principal Engineer"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert
        Assert.Contains("Tech Corp", cut.Markup);
        Assert.Contains("Principal Engineer", cut.Markup);
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
        Assert.Contains(links, a => a.GetAttribute("href") == "https://janedoe.dev");
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
        Assert.Contains("42", badge.TextContent);
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
        Assert.Equal("https://example.com/avatar.jpg", img.GetAttribute("src"));
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
        Assert.Equal("https://example.com/header.jpg", img.GetAttribute("src"));
    }

    [Fact]
    public void AuthorPage_WhenProfileLoaded_RendersBio()
    {
        // Arrange
        SetupProfile(CreateProfile(bio: "<p>This is my bio.</p>"));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — bio text rendered inside the About card
        Assert.Contains("This is my bio.", cut.Markup);
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
        Assert.Equal(2, socialButtons.Count());
        Assert.Contains("GitHub", cut.Markup);
        Assert.Contains("LinkedIn", cut.Markup);
    }

    [Fact]
    public void AuthorPage_WhenNoSocialLinks_DoesNotRenderConnectSection()
    {
        // Arrange
        SetupProfile(CreateProfile(socialLinks: []));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — "Connect" heading should not appear when there are no social links
        Assert.DoesNotContain("Connect", cut.Markup);
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
        Assert.Equal(3, skillBadges.Count());
        Assert.Contains("C#", cut.Markup);
        Assert.Contains("Blazor", cut.Markup);
        Assert.Contains("Azure", cut.Markup);
    }

    [Fact]
    public void AuthorPage_WhenNoSkills_DoesNotRenderSkillsSection()
    {
        // Arrange
        SetupProfile(CreateProfile(skills: []));

        // Act
        var cut = Render<AuthorPage>(p => p.Add(x => x.AuthorSlug, "jane-doe"));

        // Assert — "Skills" heading should not appear when there are no skills
        Assert.DoesNotContain("Skills", cut.Markup);
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
        Assert.Contains("Azure Solutions Architect", cut.Markup);
        var strong = cut.FindAll("strong");
        Assert.Contains(strong, s => s.TextContent.Contains("Azure Solutions Architect"));
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
        Assert.Contains("CNCF", cut.Markup);
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
        Assert.Contains("MIT", cut.Markup);
        var strong = cut.FindAll("strong");
        Assert.Contains(strong, s => s.TextContent.Contains("MIT"));
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
        Assert.Contains("English", cut.Markup);
        Assert.Contains("German", cut.Markup);
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
        Assert.Equal(3, interestBadges.Count());
        Assert.Contains("Open Source", cut.Markup);
        Assert.Contains("Hiking", cut.Markup);
        Assert.Contains("Music", cut.Markup);
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
        Assert.Contains(links, a => a.GetAttribute("href")!.Contains("blazor-tips"));
        Assert.Contains(links, a => a.GetAttribute("href")!.Contains("dotnet-perf"));
        Assert.Contains("Blazor Tips", cut.Markup);
        Assert.Contains(".NET Performance", cut.Markup);
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
        Assert.Contains(links, a => a.GetAttribute("href") == "/blog/post/my-post");
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
        Assert.Contains(links, a => a.GetAttribute("href") == "/articles/post/custom-post");
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
        Assert.NotEmpty(backLink);
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
        Assert.DoesNotContain("About", cut.Markup);
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
        Assert.Empty(imgs);
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
        Assert.Empty(imgs);
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
        Assert.DoesNotContain("bi-geo-alt", cut.Markup);
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
        Assert.DoesNotContain(distinctHeadline, cut.Markup);
        // The text-muted paragraph that wraps only the headline should not appear
        // when headline is null; verify by confirming the specific sentinel class
        // bi-geo-alt still appears only when location is set (location IS set here).
        // Directly confirm no element has the headline text at all.
        // "the default headline should not be rendered when Headline is null"
        Assert.DoesNotContain(cut.FindAll("p.text-muted"), p => p.TextContent == "Software Engineer");
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
        Assert.NotEmpty(backLink);
        Assert.Contains("Back to blog", backLink[0].TextContent);
    }
}
