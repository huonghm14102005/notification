using System.Globalization;
using System.Text;

namespace Notification.Application.Notifications;

public sealed record NotificationListFilter(string? Status, string? Channel, DateTimeOffset? From, DateTimeOffset? To,
    Guid? SourceDeviceId, Guid? ApiKeyId);
public sealed record NotificationListQuery(Guid TenantId, AuthCaller Caller, NotificationListFilter Filter, int Limit,
    DateTimeOffset? CursorCreatedAt, Guid? CursorId);
public sealed record NotificationDeliveryListItem(Guid Id, string Channel, string Target, string? TargetRef,
    string Status, int AttemptCount, string? ErrorCode);
public sealed record NotificationListItem(Guid Id, Guid SourceDeviceId, Guid ApiKeyId, string ProducerName,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? CompletedAt,
    IReadOnlyList<NotificationDeliveryListItem> Deliveries);
public sealed record NotificationListPage(IReadOnlyList<NotificationListItem> Items, string? NextCursor);

public static class NotificationListCursor
{
    public static string Encode(DateTimeOffset createdAt, Guid id) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"1|{createdAt:O}|{id}"));

    public static (DateTimeOffset? CreatedAt, Guid? Id) Decode(string? cursor)
    {
        if (cursor is null) return (null, null);
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (parts.Length != 3 || parts[0] != "1") throw new FormatException();
            return (DateTimeOffset.ParseExact(parts[1], "O", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind), Guid.Parse(parts[2]));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new NotificationOperationException("INVALID_CURSOR");
        }
    }
}
