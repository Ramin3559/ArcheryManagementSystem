using EShooting.Application.Common.Interfaces;
using EShooting.Infrastructure.Persistence;
using EShooting.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EShooting.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Infrastructure servislerini, EF Core DbContext-i ve repository implementasiyalarini qeydiyyatdan kecirir.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<EShootingDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
        });

        services.AddScoped<EShootingDbInitializer>();
        services.AddScoped<ITrainingCenterRepository, SqlTrainingCenterRepository>();
        return services;
    }
}
