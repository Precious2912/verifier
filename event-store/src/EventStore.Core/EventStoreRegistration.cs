using JasperFx;
using JasperFx.Events;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.Core;

public static class EventStoreRegistration
{
    public static IServiceCollection AddEventStore(this IServiceCollection services, string connectionString)
    {
        services.AddMarten(options =>
        {
            options.Connection(connectionString);
            options.Events.StreamIdentity = StreamIdentity.AsString;

            options.DatabaseSchemaName = "event_store";
            options.AutoCreateSchemaObjects = AutoCreate.All;
        });

        return services;
    }
}