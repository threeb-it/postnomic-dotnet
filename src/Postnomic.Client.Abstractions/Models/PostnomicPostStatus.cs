using System.Text.Json.Serialization;

namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// The lifecycle state of a post, as reported by the authoring API. Mirrors the API's own
/// <c>PostStatus</c> enum; serialized as a string (e.g. <c>"Published"</c>) to match the
/// <c>JsonStringEnumConverter</c> the server applies.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PostnomicPostStatus
{
    /// <summary>The post is being composed and is not visible to readers.</summary>
    Draft,

    /// <summary>The post has been submitted for review and is awaiting approval from assigned reviewers.</summary>
    InReview,

    /// <summary>The post is approved and waiting for its scheduled publish date to arrive.</summary>
    Scheduled,

    /// <summary>The post is live and visible to readers.</summary>
    Published,

    /// <summary>The post was previously published but has been taken offline (manually or by schedule).</summary>
    Unpublished,

    /// <summary>The post has been archived and is no longer actively managed or displayed.</summary>
    Archived
}
