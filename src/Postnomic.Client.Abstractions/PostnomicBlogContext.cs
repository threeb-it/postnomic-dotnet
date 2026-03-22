namespace Postnomic.Client.Abstractions;

/// <summary>
/// Provides the blog service and options for a specific named blog within a multi-blog setup.
/// This class is cascaded through the Blazor component tree by <c>PostnomicBlogScope</c>.
/// </summary>
public sealed class PostnomicBlogContext
{
    /// <summary>
    /// The registered name of this blog (the key passed to
    /// <c>AddPostnomicBlog(string name, ...)</c>).
    /// </summary>
    public required string BlogName { get; init; }

    /// <summary>
    /// The <see cref="IPostnomicBlogService"/> instance configured for this blog.
    /// </summary>
    public required IPostnomicBlogService BlogService { get; init; }

    /// <summary>
    /// The <see cref="PostnomicClientOptions"/> for this blog.
    /// </summary>
    public required PostnomicClientOptions Options { get; init; }
}
