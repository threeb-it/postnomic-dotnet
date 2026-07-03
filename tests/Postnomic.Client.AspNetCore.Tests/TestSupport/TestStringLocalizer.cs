using Microsoft.Extensions.Localization;

namespace Postnomic.Client.AspNetCore.Tests.TestSupport;

/// <summary>
/// Minimal in-memory <see cref="IStringLocalizer{T}"/> double for unit tests that assert on the
/// English text of a localized page-model message (e.g. <c>PostModel.CommentSubmit*Message</c>)
/// without wiring up the real resx-backed localizer. Falls back to echoing the key itself for any
/// name not present in <paramref name="values"/>.
/// </summary>
internal sealed class TestStringLocalizer<T>(IReadOnlyDictionary<string, string> values) : IStringLocalizer<T>
{
    public LocalizedString this[string name]
    {
        get
        {
            var found = values.TryGetValue(name, out var value);
            return new LocalizedString(name, value ?? name, resourceNotFound: !found);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var found = values.TryGetValue(name, out var format);
            var value = found ? string.Format(format!, arguments) : name;
            return new LocalizedString(name, value, resourceNotFound: !found);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        values.Select(kv => new LocalizedString(kv.Key, kv.Value));
}

/// <summary>
/// Factory helpers for <see cref="TestStringLocalizer{T}"/> instances mirroring the real
/// English resx content shipped for the Blog Area page models, so unit tests can assert on
/// message text without loading the actual resource assembly.
/// </summary>
internal static class TestStringLocalizers
{
    /// <summary>An <see cref="IStringLocalizer{T}"/> mirroring <c>PostModel.en.resx</c>.</summary>
    public static IStringLocalizer<Postnomic.Client.AspNetCore.Areas.Blog.Pages.PostModel> Post() =>
        new TestStringLocalizer<Postnomic.Client.AspNetCore.Areas.Blog.Pages.PostModel>(
            new Dictionary<string, string>
            {
                ["CommentSubmitValidationError"] = "Please correct the errors below and try again.",
                ["CommentSubmitError"] = "Your comment could not be submitted. Please try again later.",
                ["CommentSubmitModerated"] = "Thank you! Your comment has been submitted and is awaiting moderation.",
                ["CommentSubmitSuccess"] = "Thank you! Your comment has been published.",
                ["FieldSubjectRequired"] = "Subject is required.",
                ["FieldFirstNameRequired"] = "First name is required.",
                ["FieldLastNameRequired"] = "Last name is required.",
                ["FieldEmailRequired"] = "Email address is required.",
                ["FieldPhoneRequired"] = "Phone number is required.",
            });
}
