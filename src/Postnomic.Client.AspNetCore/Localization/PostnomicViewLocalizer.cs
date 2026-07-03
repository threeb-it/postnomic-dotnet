using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Postnomic.Client.AspNetCore.Localization;

/// <summary>
/// Drop-in replacement for the default ASP.NET Core <see cref="ViewLocalizer"/> that fixes
/// resource resolution for Razor views compiled into this Razor Class Library.
/// </summary>
/// <remarks>
/// <para>
/// The stock <c>ViewLocalizer</c> resolves resources using <see cref="IWebHostEnvironment"/>'s
/// <c>ApplicationName</c> as the resource-searching assembly, which is the *consuming* application's
/// entry assembly — not
/// the assembly the view (and its co-located <c>.resx</c> files) actually ships in. For a view compiled
/// into <c>Postnomic.Client.AspNetCore</c> and consumed by a host app such as ThreeBIT's website, that
/// means the stock localizer would look for Blog Area strings inside the host's own assembly and never
/// find them.
/// </para>
/// <para>
/// This implementation mirrors the stock <c>ViewLocalizer</c> exactly, except that for views under the
/// Blog Area (<c>Areas/Blog/Pages/…</c>) it always resolves resources against this assembly
/// (<see cref="PostnomicAspNetCoreExtensions"/>'s assembly) instead of the host's. Any other view — e.g.
/// one belonging to the host application itself — falls back to the standard host-assembly behavior,
/// so registering this type does not change localization for views outside the Blog Area.
/// </para>
/// </remarks>
public sealed class PostnomicViewLocalizer : IViewLocalizer, IViewContextAware
{
    private static readonly string BlogAreaAssemblyName = typeof(PostnomicAspNetCoreExtensions).Assembly.GetName().Name!;

    private readonly IHtmlLocalizerFactory _localizerFactory;
    private readonly IHtmlLocalizerFactory _blogAreaLocalizerFactory;
    private readonly string _hostApplicationName;
    private IHtmlLocalizer _localizer = null!;

    /// <summary>Creates the localizer. Invoked by DI; not intended for direct instantiation.</summary>
    public PostnomicViewLocalizer(
        IHtmlLocalizerFactory localizerFactory,
        IWebHostEnvironment webHostEnvironment,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(localizerFactory);
        ArgumentNullException.ThrowIfNull(webHostEnvironment);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _localizerFactory = localizerFactory;
        _hostApplicationName = webHostEnvironment.ApplicationName;

        // Blog Area resources must resolve independent of whatever ResourcesPath the consuming
        // host configures via services.AddLocalization(o => o.ResourcesPath = "..."). The
        // injected _localizerFactory above is backed by the HOST's ResourceManagerStringLocalizerFactory,
        // built from the host's LocalizationOptions.ResourcesPath -- if the host sets that to e.g.
        // "Resources" (a common convention for its OWN page resx), the factory prepends
        // "Resources." to the base resource name it searches for. But this assembly's Blog Area
        // resx files are embedded WITHOUT that segment
        // (Postnomic.Client.AspNetCore.Areas.Blog.Pages.Index, not
        // Postnomic.Client.AspNetCore.Resources.Areas.Blog.Pages.Index), so the lookup misses and
        // ResourceManagerStringLocalizer falls back to returning the raw key name. This private
        // factory is always constructed with an empty ResourcesPath, so it always searches for the
        // Blog Area's resources exactly where they are embedded, regardless of host configuration.
        var blogAreaLocalizationOptions = Options.Create(new LocalizationOptions { ResourcesPath = "" });
        var blogAreaStringLocalizerFactory = new ResourceManagerStringLocalizerFactory(blogAreaLocalizationOptions, loggerFactory);
        _blogAreaLocalizerFactory = new HtmlLocalizerFactory(blogAreaStringLocalizerFactory);
    }

    /// <inheritdoc />
    public void Contextualize(ViewContext viewContext)
    {
        ArgumentNullException.ThrowIfNull(viewContext);

        var path = viewContext.ExecutingFilePath;
        if (string.IsNullOrEmpty(path))
        {
            path = viewContext.View.Path;
        }

        var relativePath = BuildRelativePath(path);

        _localizer = IsBlogAreaView(path)
            ? _blogAreaLocalizerFactory.Create(relativePath, BlogAreaAssemblyName)
            : _localizerFactory.Create(relativePath, _hostApplicationName);
    }

    /// <inheritdoc />
    public LocalizedHtmlString this[string key] => _localizer[key];

    /// <inheritdoc />
    public LocalizedHtmlString this[string key, params object[] arguments] => _localizer[key, arguments];

    /// <inheritdoc />
    public LocalizedString GetString(string name) => _localizer.GetString(name);

    /// <inheritdoc />
    public LocalizedString GetString(string name, params object[] arguments) => _localizer.GetString(name, arguments);

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        _localizer.GetAllStrings(includeParentCultures);

    private static bool IsBlogAreaView(string executingFilePath) =>
        executingFilePath.Replace('\\', '/').Contains("/Areas/Blog/Pages/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Mirrors the private <c>ViewLocalizer.BuildRelativePath</c> algorithm: given a view path such as
    /// <c>/Areas/Blog/Pages/Index.cshtml</c>, produces <c>Areas.Blog.Pages.Index</c>.
    /// </summary>
    private static string BuildRelativePath(string executingFilePath)
    {
        var extension = Path.GetExtension(executingFilePath);
        if (!string.IsNullOrEmpty(extension))
        {
            executingFilePath = executingFilePath[..^extension.Length];
        }

        if (executingFilePath.StartsWith('/') || executingFilePath.StartsWith('\\'))
        {
            executingFilePath = executingFilePath[1..];
        }

        return executingFilePath.Replace('/', '.').Replace('\\', '.').Replace(' ', '_');
    }
}
