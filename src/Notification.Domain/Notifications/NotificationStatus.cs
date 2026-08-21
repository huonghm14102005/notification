namespace Notification.Domain.Notifications;

public static class NotificationStatus
{
    public const string Accepted = "accepted";
    public const string Processing = "processing";
    public const string Delivered = "delivered";
    public const string PartiallyDelivered = "partially_delivered";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}
