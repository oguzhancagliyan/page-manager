using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PageManager.Infrastructure.Cache;
using PageManager.Infrastructure.Persistence;

namespace PageManager.Infrastructure.DI;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new Exception("Could not find connection string");
        }

        services.AddDbContextPool<AppDbContext>(o => o.UseNpgsql(conn));

        services.AddMemoryCache();
        services.AddSingleton<ICacheManager, CacheManager>();


        services.AddOpenTelemetry()
            .WithTracing(tp =>
            {
                tp.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("PagesApi"))
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.client_ip",
                                request.HttpContext.Connection.RemoteIpAddress?.ToString());
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = true)
                    .AddSource("Feature.Pages.ArchiveAndMayPublish")
                    .SetSampler(new AlwaysOnSampler())
                    .AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri("http://otel-collector:4317");
                        opt.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });
            });

        return services;
    }
}