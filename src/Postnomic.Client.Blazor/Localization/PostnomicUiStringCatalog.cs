namespace Postnomic.Client.Blazor.Localization;

/// <summary>
/// The built-in translations for every UI chrome string the Postnomic Blazor components render
/// (as opposed to a blog's own post/author content, which is never touched here). English is the
/// baseline — every key that exists anywhere in the catalog exists in English — and is also the
/// fallback used when a language or key has no translation. See
/// <see cref="Postnomic.Client.Abstractions.PostnomicUiStringOverrides"/> for how a consumer can
/// replace or extend this catalog without forking the package.
/// </summary>
/// <remarks>
/// A plain dictionary rather than <c>IStringLocalizer</c>/.resx: this package is a distributable
/// SDK, not a hosting application, and a dictionary keeps localization file-count and packaging
/// (satellite assemblies, resource embedding) out of consumers' build entirely. Adding a third
/// language is a data change here, not a code change.
/// </remarks>
internal static class PostnomicUiStringCatalog
{
    /// <summary>The language used when no more specific translation is available.</summary>
    public const string DefaultLanguage = "en";

    // NOTE: static field initializers run in textual declaration order, so `English` and `German`
    // must be declared (and therefore initialized) above `Languages` — a `Languages` field placed
    // first would capture both as null, since neither would have run yet.

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // ── Shared across pages/components ──────────────────────────────────────────────
        ["Common.Loading"] = "Loading…",
        ["Common.NoPostsFound"] = "No posts found.",
        ["Common.BackToBlog"] = "← Back to blog",
        ["Common.ClearFilter"] = "✕ Clear filter",
        ["Common.Post"] = "post",
        ["Common.Posts"] = "posts",
        ["Common.Comment"] = "comment",
        ["Common.Comments"] = "comments",
        ["Common.Submitting"] = "Submitting…",
        ["Common.Anonymous"] = "Anonymous",
        ["Common.CancelReply"] = "Cancel reply",
        ["Common.Reply"] = "↩ Reply",
        ["Common.Form.FirstName"] = "First name",
        ["Common.Form.LastName"] = "Last name",
        ["Common.Form.Email"] = "Email",
        ["Common.Form.Phone"] = "Phone",
        ["Common.Form.Subject"] = "Subject",

        // ── BlogPage ─────────────────────────────────────────────────────────────────────
        ["BlogPage.ClearAllFilters"] = "✕ Clear all",
        ["BlogPage.FilteredByLabel"] = "Filtered by:",
        ["BlogPage.ReadMore"] = "Read More →",
        ["BlogPage.LoadingPosts"] = "Loading posts…",
        ["BlogPage.Pager.Previous"] = "← Previous",
        ["BlogPage.Pager.Next"] = "Next →",

        // ── PostPage ─────────────────────────────────────────────────────────────────────
        ["PostPage.PoweredBy"] = "Powered by",
        ["PostPage.TaglineSuffix"] = "— The developer-first headless blog platform.",
        ["PostPage.UpgradeToRemoveBanner"] = "Upgrade to remove this banner",
        ["PostPage.LeaveAComment"] = "Leave a comment",
        ["PostPage.CommentsHeading"] = "Comments ({0})",
        ["PostPage.NoCommentsYet"] = "No comments yet.",
        ["PostPage.Form.CommentLabel"] = "Comment",
        ["PostPage.PostComment"] = "Post comment",
        ["PostPage.CommentsClosed"] = "Comments are closed for this post.",
        ["PostPage.CommentPostedModeration"] = "Your comment has been submitted and is awaiting moderation.",
        ["PostPage.CommentPosted"] = "Your comment has been posted!",
        ["PostPage.CommentPostFailed"] = "Failed to post comment. Please try again.",

        // ── CommentView (nested replies) ────────────────────────────────────────────────
        ["CommentView.Form.ReplyLabel"] = "Reply",
        ["CommentView.PostReply"] = "Post reply",
        ["CommentView.ReplyPostedModeration"] = "Your reply has been submitted and is awaiting moderation.",
        ["CommentView.ReplyPosted"] = "Your reply has been posted!",
        ["CommentView.ReplyPostFailed"] = "Failed to post reply. Please try again.",

        // ── AuthorPage ───────────────────────────────────────────────────────────────────
        ["AuthorPage.Connect"] = "Connect",
        ["AuthorPage.Website"] = "Website",
        ["AuthorPage.Skills"] = "Skills",
        ["AuthorPage.Languages"] = "Languages",
        ["AuthorPage.Interests"] = "Interests",
        ["AuthorPage.About"] = "About",
        ["AuthorPage.Certifications"] = "Certifications",
        ["AuthorPage.Issued"] = "Issued",
        ["AuthorPage.Expires"] = "Expires",
        ["AuthorPage.ViewCredential"] = "View credential",
        ["AuthorPage.Education"] = "Education",
        ["AuthorPage.Present"] = "Present",
        ["AuthorPage.RecentPosts"] = "Recent Posts",
        ["AuthorPage.At"] = " at ",
        ["AuthorPage.HeaderImageAlt"] = "{0} header",

        // ── Sidebar: SearchBox ───────────────────────────────────────────────────────────
        ["SearchBox.Title"] = "Search",
        ["SearchBox.Placeholder"] = "Search posts…",

        // ── Sidebar: TagCloud ────────────────────────────────────────────────────────────
        ["TagCloud.Title"] = "Tags",
        ["TagCloud.NoneFound"] = "No tags found.",

        // ── Sidebar: CategoryList ────────────────────────────────────────────────────────
        ["CategoryList.Title"] = "Categories",
        ["CategoryList.NoneFound"] = "No categories found.",

        // ── Sidebar: AuthorList ──────────────────────────────────────────────────────────
        ["AuthorList.Title"] = "Authors",
        ["AuthorList.NoneFound"] = "No authors found.",

        // ── Sidebar: TopCommentedPosts ───────────────────────────────────────────────────
        ["TopCommentedPosts.Title"] = "Top Commented Posts",

        // ── Sidebar: MostReadPosts ───────────────────────────────────────────────────────
        ["MostReadPosts.Title"] = "Most Read Posts",

        // ── Sidebar: EstimatedReadTime ───────────────────────────────────────────────────
        ["EstimatedReadTime.Title"] = "Estimated Read Time",
        ["EstimatedReadTime.NoContent"] = "No content available.",
        ["EstimatedReadTime.Minute"] = "minute",
        ["EstimatedReadTime.Minutes"] = "minutes",
        ["EstimatedReadTime.Words"] = "words",

        // ── Sidebar: PostnomicPromo ──────────────────────────────────────────────────────
        ["PostnomicPromo.Description"] = "The developer-first headless blog platform. Add a full-featured blog to your app in minutes.",
        ["PostnomicPromo.LearnMore"] = "Learn More",
        ["PostnomicPromo.ViewPricing"] = "View Pricing",
        ["PostnomicPromo.UpgradeToRemoveAds"] = "Upgrade to remove ads",
    };

    private static readonly IReadOnlyDictionary<string, string> German = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // ── Shared across pages/components ──────────────────────────────────────────────
        ["Common.Loading"] = "Wird geladen…",
        ["Common.NoPostsFound"] = "Keine Beiträge gefunden.",
        ["Common.BackToBlog"] = "← Zurück zum Blog",
        ["Common.ClearFilter"] = "✕ Filter zurücksetzen",
        ["Common.Post"] = "Beitrag",
        ["Common.Posts"] = "Beiträge",
        ["Common.Comment"] = "Kommentar",
        ["Common.Comments"] = "Kommentare",
        ["Common.Submitting"] = "Wird gesendet…",
        ["Common.Anonymous"] = "Anonym",
        ["Common.CancelReply"] = "Antwort abbrechen",
        ["Common.Reply"] = "↩ Antworten",
        ["Common.Form.FirstName"] = "Vorname",
        ["Common.Form.LastName"] = "Nachname",
        ["Common.Form.Email"] = "E-Mail",
        ["Common.Form.Phone"] = "Telefon",
        ["Common.Form.Subject"] = "Betreff",

        // ── BlogPage ─────────────────────────────────────────────────────────────────────
        ["BlogPage.ClearAllFilters"] = "✕ Alle Filter zurücksetzen",
        ["BlogPage.FilteredByLabel"] = "Gefiltert nach:",
        ["BlogPage.ReadMore"] = "Weiterlesen →",
        ["BlogPage.LoadingPosts"] = "Beiträge werden geladen…",
        ["BlogPage.Pager.Previous"] = "← Zurück",
        ["BlogPage.Pager.Next"] = "Weiter →",

        // ── PostPage ─────────────────────────────────────────────────────────────────────
        ["PostPage.PoweredBy"] = "Bereitgestellt von",
        ["PostPage.TaglineSuffix"] = "— die entwicklerfreundliche Headless-Blog-Plattform.",
        ["PostPage.UpgradeToRemoveBanner"] = "Upgraden, um diesen Hinweis zu entfernen",
        ["PostPage.LeaveAComment"] = "Kommentar schreiben",
        ["PostPage.CommentsHeading"] = "Kommentare ({0})",
        ["PostPage.NoCommentsYet"] = "Noch keine Kommentare.",
        ["PostPage.Form.CommentLabel"] = "Kommentar",
        ["PostPage.PostComment"] = "Kommentar absenden",
        ["PostPage.CommentsClosed"] = "Für diesen Beitrag sind keine Kommentare mehr möglich.",
        ["PostPage.CommentPostedModeration"] = "Dein Kommentar wurde übermittelt und wartet auf Freigabe.",
        ["PostPage.CommentPosted"] = "Dein Kommentar wurde veröffentlicht!",
        ["PostPage.CommentPostFailed"] = "Der Kommentar konnte nicht gesendet werden. Bitte versuch es erneut.",

        // ── CommentView (nested replies) ────────────────────────────────────────────────
        ["CommentView.Form.ReplyLabel"] = "Antwort",
        ["CommentView.PostReply"] = "Antwort absenden",
        ["CommentView.ReplyPostedModeration"] = "Deine Antwort wurde übermittelt und wartet auf Freigabe.",
        ["CommentView.ReplyPosted"] = "Deine Antwort wurde veröffentlicht!",
        ["CommentView.ReplyPostFailed"] = "Die Antwort konnte nicht gesendet werden. Bitte versuch es erneut.",

        // ── AuthorPage ───────────────────────────────────────────────────────────────────
        ["AuthorPage.Connect"] = "Kontakt",
        ["AuthorPage.Website"] = "Website",
        ["AuthorPage.Skills"] = "Fähigkeiten",
        ["AuthorPage.Languages"] = "Sprachen",
        ["AuthorPage.Interests"] = "Interessen",
        ["AuthorPage.About"] = "Über mich",
        ["AuthorPage.Certifications"] = "Zertifizierungen",
        ["AuthorPage.Issued"] = "Ausgestellt",
        ["AuthorPage.Expires"] = "Gültig bis",
        ["AuthorPage.ViewCredential"] = "Nachweis ansehen",
        ["AuthorPage.Education"] = "Ausbildung",
        ["AuthorPage.Present"] = "Heute",
        ["AuthorPage.RecentPosts"] = "Neueste Beiträge",
        ["AuthorPage.At"] = " bei ",
        ["AuthorPage.HeaderImageAlt"] = "{0} – Titelbild",

        // ── Sidebar: SearchBox ───────────────────────────────────────────────────────────
        ["SearchBox.Title"] = "Suche",
        ["SearchBox.Placeholder"] = "Beiträge durchsuchen…",

        // ── Sidebar: TagCloud ────────────────────────────────────────────────────────────
        ["TagCloud.Title"] = "Schlagwörter",
        ["TagCloud.NoneFound"] = "Keine Schlagwörter gefunden.",

        // ── Sidebar: CategoryList ────────────────────────────────────────────────────────
        ["CategoryList.Title"] = "Kategorien",
        ["CategoryList.NoneFound"] = "Keine Kategorien gefunden.",

        // ── Sidebar: AuthorList ──────────────────────────────────────────────────────────
        ["AuthorList.Title"] = "Autor:innen",
        ["AuthorList.NoneFound"] = "Keine Autor:innen gefunden.",

        // ── Sidebar: TopCommentedPosts ───────────────────────────────────────────────────
        ["TopCommentedPosts.Title"] = "Meistkommentierte Beiträge",

        // ── Sidebar: MostReadPosts ───────────────────────────────────────────────────────
        ["MostReadPosts.Title"] = "Meistgelesene Beiträge",

        // ── Sidebar: EstimatedReadTime ───────────────────────────────────────────────────
        ["EstimatedReadTime.Title"] = "Geschätzte Lesezeit",
        ["EstimatedReadTime.NoContent"] = "Kein Inhalt verfügbar.",
        ["EstimatedReadTime.Minute"] = "Minute",
        ["EstimatedReadTime.Minutes"] = "Minuten",
        ["EstimatedReadTime.Words"] = "Wörter",

        // ── Sidebar: PostnomicPromo ──────────────────────────────────────────────────────
        ["PostnomicPromo.Description"] = "Die entwicklerfreundliche Headless-Blog-Plattform. Füge deiner App in wenigen Minuten einen voll ausgestatteten Blog hinzu.",
        ["PostnomicPromo.LearnMore"] = "Mehr erfahren",
        ["PostnomicPromo.ViewPricing"] = "Preise ansehen",
        ["PostnomicPromo.UpgradeToRemoveAds"] = "Upgraden, um Werbung zu entfernen",
    };

    /// <summary>Built-in translations, keyed by two-letter language code (case-insensitive).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Languages =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultLanguage] = English,
            ["de"] = German,
        };
}
