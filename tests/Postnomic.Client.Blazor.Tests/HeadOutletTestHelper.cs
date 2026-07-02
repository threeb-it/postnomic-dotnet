using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Postnomic.Client.Blazor.Tests;

/// <summary>
/// bUnit does not mount a <see cref="HeadOutlet"/> by default, so a component's
/// <c>&lt;HeadContent&gt;</c> markup never reaches an isolated render's <c>Markup</c> — the
/// section content is registered with the renderer's internal section registry but nothing
/// subscribes to it. Wrapping the component-under-test alongside a real <see cref="HeadOutlet"/>
/// (both rendered by the same bUnit renderer, as would happen in a real Blazor app's root layout)
/// makes the emitted head tags observable via the returned render's <c>Markup</c>, just like they
/// would be in a real app.
/// </summary>
internal static class HeadOutletTestHelper
{
    /// <summary>
    /// Wraps <paramref name="content"/> so it renders alongside a <see cref="HeadOutlet"/>.
    /// Callers must set <c>JSInterop.Mode = JSRuntimeMode.Loose</c> beforehand, since
    /// <see cref="HeadOutlet"/> performs a best-effort JS interop call on first render.
    /// </summary>
    public static RenderFragment WithHeadOutlet(RenderFragment content) => builder =>
    {
        builder.OpenComponent<HeadOutlet>(0);
        builder.CloseComponent();
        builder.AddContent(1, content);
    };
}
