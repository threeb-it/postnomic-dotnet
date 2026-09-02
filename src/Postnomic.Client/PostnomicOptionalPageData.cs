using Microsoft.Extensions.Logging;

namespace Postnomic.Client;

/// <summary>
/// Loads a piece of <b>decorative</b> page data — a sidebar widget — so that a failing widget
/// degrades to an empty fallback instead of taking the whole page down with it. Shared by both
/// hosting models: the Razor Pages Area in <c>Postnomic.Client.AspNetCore</c> and the Blazor
/// components in <c>Postnomic.Client.Blazor</c>.
/// <para>
/// The blog page fans out to several API calls in parallel. Before this helper existed they all
/// ran under a single bare <c>Task.WhenAll</c>, so one failing tag list turned a partially
/// available backend into a hard 500 for the visitor (and one error per widget in the host's
/// error tracker). Essential data — the post list, the blog metadata, the post itself — is
/// deliberately <i>not</i> routed through here: when that fails, the page must still fail in
/// whatever way that hosting model already failed.
/// </para>
/// </summary>
public static class PostnomicOptionalPageData
{
    /// <summary>
    /// Invokes <paramref name="load"/> and returns its result, or <paramref name="fallback"/> when
    /// it fails. The work is started synchronously by the call, so callers keep their parallelism
    /// by starting every widget before awaiting any of them.
    /// </summary>
    /// <typeparam name="T">The widget's data type.</typeparam>
    /// <param name="load">Starts the widget's API call.</param>
    /// <param name="fallback">The value to render when the call fails (an empty collection).</param>
    /// <param name="widget">Widget name, used in the warning log only.</param>
    /// <param name="logger">Logger for the page; may be <see langword="null"/> in tests.</param>
    /// <param name="cancellationToken">
    /// The request's cancellation token. A cancellation raised by <i>this</i> token means the
    /// visitor went away — it is rethrown rather than reported as a widget failure. A
    /// <see cref="OperationCanceledException"/> that is <i>not</i> this token (an
    /// <see cref="System.Net.Http.HttpClient"/> timeout, for instance) is a genuine widget failure
    /// and degrades like any other.
    /// </param>
    public static async Task<T> LoadAsync<T>(
        Func<Task<T>> load,
        T fallback,
        string widget,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await load().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Postnomic: the '{Widget}' widget could not be loaded and was rendered empty. "
                + "The rest of the page is unaffected.",
                widget);
            return fallback;
        }
    }
}
