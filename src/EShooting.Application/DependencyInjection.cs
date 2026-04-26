using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EShooting.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Application qatinin servislerini ve MediatR handlerlerini qeydiyyatdan kecirir.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
