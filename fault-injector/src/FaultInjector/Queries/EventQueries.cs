namespace FaultInjector.Queries;

public static class EventQueries
{
    // Shared queries for event fault types (dropped, duplicate, incorrect amount)
    public const string GetRandomTransactionEvent = """
        SELECT data ->> 'Reference' AS Reference, type AS Type,
               id::text AS EventId, stream_id AS StreamId, data ->> 'Amount' AS Amount
        FROM event_store.mt_events
        WHERE type IN ('account_debited', 'account_credited')
        ORDER BY random() LIMIT 1;
        """;

    public const string DeleteEventById = """
        DELETE FROM event_store.mt_events
        WHERE id = @id::uuid;
        """;

    // Insert a full event row — used by fault: drop, mode: revert and fault: duplicate, mode:inject.
    public const string InsertEvent = """
        INSERT INTO event_store.mt_events
            (seq_id, id, stream_id, version, data, type, timestamp, tenant_id, mt_dotnet_type, is_archived)
        VALUES (@SeqId, @Id, @StreamId, @Version, @Data::jsonb, @Type,
                @Timestamp, @TenantId, @DotNetType, @IsArchived);
        """;

    // Dropped event
    public const string FindEventByReference = """
        SELECT seq_id AS SeqId, id::text AS EventId, stream_id AS StreamId,
               version AS Version, data::text AS Data, type AS Type,
               timestamp AS Timestamp, tenant_id AS TenantId,
               mt_dotnet_type AS DotNetType, is_archived AS IsArchived
        FROM event_store.mt_events
        WHERE type = @eventType AND data ->> 'Reference' = @reference
        LIMIT 1;
        """;

    // Duplicate event
    public const string GetEventForDuplication = """
        SELECT stream_id, version, data::text AS data, type, timestamp, tenant_id,
               mt_dotnet_type, is_archived
        FROM event_store.mt_events
        WHERE type = @eventType AND data ->> 'Reference' = @reference
        LIMIT 1;
        """;

    public const string GetNextSeqId = """
        SELECT COALESCE(MAX(seq_id), 0) + 1 FROM event_store.mt_events;
        """;

    public const string GetNextVersionForStream = """
        SELECT COALESCE(MAX(version), 0) + 1
        FROM event_store.mt_events
        WHERE stream_id = @streamId;
        """;

    // Incorrect amount
    public const string FindEventAmountByReference = """
        SELECT id::text AS EventId, stream_id AS StreamId, data ->> 'Amount' AS Amount
        FROM event_store.mt_events
        WHERE type = @eventType AND data ->> 'Reference' = @reference
        LIMIT 1;
        """;


    // Overwrite the Amount inside an event's JSONB — used by fault: incorrect amount, mode:inject (corrupt) and revert (restore).
    public const string UpdateEventAmount = """
        UPDATE event_store.mt_events
        SET data = jsonb_set(data, '{Amount}', to_jsonb(@amt::numeric))
        WHERE id = @id::uuid;
        """;
}