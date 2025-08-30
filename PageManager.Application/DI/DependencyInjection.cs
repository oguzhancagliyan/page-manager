using PageManager.Application.Features.Pages.ArchiveAndMayPublishFeature;

namespace PageManager.Application.DI;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssemblyContaining<ArchiveAndMayPublishCommandValidator>();
        return services;
    }
}