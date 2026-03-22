namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// Full profile of a blog author, including bio, social links, certifications, education, and recent posts.
/// </summary>
public record PostnomicAuthorProfile
{
    /// <summary>The full display name of the author.</summary>
    public required string Name { get; init; }

    /// <summary>The URL-friendly slug of the author. <see langword="null"/> when not set.</summary>
    public string? Slug { get; init; }

    /// <summary>A short professional headline. <see langword="null"/> when not set.</summary>
    public string? Headline { get; init; }

    /// <summary>A longer biography. <see langword="null"/> when not set.</summary>
    public string? Bio { get; init; }

    /// <summary>The author's location. <see langword="null"/> when not set.</summary>
    public string? Location { get; init; }

    /// <summary>The author's personal website URL. <see langword="null"/> when not set.</summary>
    public string? WebsiteUrl { get; init; }

    /// <summary>URL of the author's profile picture. <see langword="null"/> when not set.</summary>
    public string? ProfileImageUrl { get; init; }

    /// <summary>URL of the author's header/banner image. <see langword="null"/> when not set.</summary>
    public string? HeaderImageUrl { get; init; }

    /// <summary>The author's company or organization. <see langword="null"/> when not set.</summary>
    public string? Company { get; init; }

    /// <summary>The author's job title. <see langword="null"/> when not set.</summary>
    public string? JobTitle { get; init; }

    /// <summary>The total number of published posts by this author on the blog.</summary>
    public int PostCount { get; init; }

    /// <summary>Social media profiles. Never <see langword="null"/>; may be empty.</summary>
    public ICollection<PostnomicSocialLink> SocialLinks { get; init; } = [];

    /// <summary>Professional certifications. Never <see langword="null"/>; may be empty.</summary>
    public ICollection<PostnomicCertification> Certifications { get; init; } = [];

    /// <summary>Personal interests. Never <see langword="null"/>; may be empty.</summary>
    public ICollection<string> Interests { get; init; } = [];

    /// <summary>Technical or professional skills. Never <see langword="null"/>; may be empty.</summary>
    public ICollection<string> Skills { get; init; } = [];

    /// <summary>Educational history. Never <see langword="null"/>; may be empty.</summary>
    public ICollection<PostnomicEducation> Education { get; init; } = [];

    /// <summary>Languages spoken by the author. Never <see langword="null"/>; may be empty.</summary>
    public ICollection<PostnomicLanguage> Languages { get; init; } = [];

    /// <summary>The author's most recent published posts. Never <see langword="null"/>; may be empty.</summary>
    public ICollection<PostnomicPostSummary> RecentPosts { get; init; } = [];
}

/// <summary>A social media profile link for an author.</summary>
public record PostnomicSocialLink
{
    /// <summary>The name of the social platform (e.g. "GitHub", "LinkedIn").</summary>
    public required string Platform { get; init; }

    /// <summary>The full URL to the author's profile on the platform.</summary>
    public required string Url { get; init; }
}

/// <summary>A professional certification held by an author.</summary>
public record PostnomicCertification
{
    /// <summary>The name of the certification.</summary>
    public required string Name { get; init; }

    /// <summary>The organization that issued the certification. <see langword="null"/> when not set.</summary>
    public string? IssuingOrganization { get; init; }

    /// <summary>The date the certification was issued. <see langword="null"/> when not set.</summary>
    public DateTime? IssueDate { get; init; }

    /// <summary>The expiration date of the certification. <see langword="null"/> when not applicable.</summary>
    public DateTime? ExpirationDate { get; init; }

    /// <summary>A credential ID or certificate number. <see langword="null"/> when not set.</summary>
    public string? CredentialId { get; init; }

    /// <summary>A URL to verify the credential online. <see langword="null"/> when not set.</summary>
    public string? CredentialUrl { get; init; }
}

/// <summary>An educational entry in an author's profile.</summary>
public record PostnomicEducation
{
    /// <summary>The name of the educational institution.</summary>
    public required string Institution { get; init; }

    /// <summary>The degree obtained. <see langword="null"/> when not set.</summary>
    public string? Degree { get; init; }

    /// <summary>The field of study. <see langword="null"/> when not set.</summary>
    public string? FieldOfStudy { get; init; }

    /// <summary>The start date of the study period. <see langword="null"/> when not set.</summary>
    public DateTime? StartDate { get; init; }

    /// <summary>The end date of the study period. <see langword="null"/> when still in progress.</summary>
    public DateTime? EndDate { get; init; }
}

/// <summary>A language spoken by an author, optionally with a proficiency level.</summary>
public record PostnomicLanguage
{
    /// <summary>The name of the language (e.g. "English", "German").</summary>
    public required string Name { get; init; }

    /// <summary>Proficiency level (e.g. "Native", "Professional"). <see langword="null"/> when not set.</summary>
    public string? Proficiency { get; init; }
}
