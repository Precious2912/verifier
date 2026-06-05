using Microsoft.Extensions.DependencyInjection;
using TransactionSystem.Application.Interfaces;
using TransactionSystem.Application.Services;

namespace TransactionSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();

        return services;
    }
}