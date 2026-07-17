namespace FaultInjector.Queries;

public static class CrudQueries
{
    public const string FindEvent = """
        SELECT seq_id AS SeqId, id::text AS EventId, stream_id AS StreamId,
               version AS Version, data::text AS Data, type AS Type,
               timestamp AS Timestamp, tenant_id AS TenantId,
               mt_dotnet_type AS DotNetType, is_archived AS IsArchived
        FROM event_store.mt_events
        WHERE type = @eventType AND data ->> 'Reference' = @reference
        LIMIT 1;
        """;
}