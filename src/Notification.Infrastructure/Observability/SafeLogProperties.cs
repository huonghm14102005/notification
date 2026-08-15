namespace Notification.Infrastructure.Observability;

public static class SafeLogProperties
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "correlationId",
        "tenantId",
        "notificationId",
        "adminId",
        "producerId",
        "eventName",
        "result",
        "durationMs",
        "contentLength",
    };

    public static IReadOnlyDictionary<string, object?> Create(IEnumerable<KeyValuePair<string, object?>> values) =>
        values
            .Where(value => AllowedKeys.Contains(value.Key))
            .ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);
}
