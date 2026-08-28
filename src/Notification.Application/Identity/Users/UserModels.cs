namespace Notification.Application.Identity.Users;

public sealed record UserItem(Guid Id, string Email, string DisplayName, string Role, string Status,
    int DeviceCount, int ActiveDeviceCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? DisabledAt);
public sealed record UserPage(IReadOnlyList<UserItem> Items, string? NextCursor);
public sealed record CreateUserCommand(string Email, string Password, string? DisplayName);
public enum CreateUserResult { Success, EmailExists }
public sealed class UserOperationException(string code) : Exception(code) { public string Code { get; } = code; }
