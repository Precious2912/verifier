using CrudSystem.Application.Interfaces;
using CrudSystem.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrudSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();

        return services;
    }
}