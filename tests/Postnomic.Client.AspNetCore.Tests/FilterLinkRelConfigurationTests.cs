using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;

namespace Postnomic.Client.AspNetCore.Tests;

/// <summary>
/// <see cref="PostnomicClientOptions.FilterLinkRel"/> binds from configuration like every other
/// option, for the default and for a named blog.
/// </summary>
public class FilterLinkRelConfigurationTests
{
    [Fact]
    public void FilterLinkRel_binds_from_configuration_per_blog()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postnomic:FilterLinkRel"] = "nofollow",
                ["Blogs:Pro:FilterLinkRel"] = "nofollow ugc",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddPostnomicBlog(o => configuration.GetSection("Postnomic").Bind(o));
        services.AddPostnomicBlog("pro", o =>
        {
            o.BasePath = "/blog/pro";
            configuration.GetSection("Blogs:Pro").Bind(o);
        });
        services.AddPostnomicBlog("free", o => o.BasePath = "/blog/free");
        using var provider = services.BuildServiceProvider();

        Assert.Equal("nofollow", provider.GetRequiredService<IOptions<PostnomicClientOptions>>().Value.FilterLinkRel);
        var monitor = provider.GetRequiredService<IOptionsMonitor<PostnomicClientOptions>>();
        Assert.Equal("nofollow ugc", monitor.Get("pro").FilterLinkRel);
        Assert.Null(monitor.Get("free").FilterLinkRel);
    }
}
