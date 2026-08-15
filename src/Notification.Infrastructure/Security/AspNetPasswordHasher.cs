using Microsoft.AspNetCore.Identity;
using Notification.Application.Abstractions.Security;

namespace Notification.Infrastructure.Security;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object User = new();
    public string Hash(string password) => _hasher.HashPassword(User, password);
    public bool Verify(string hash, string password) => _hasher.VerifyHashedPassword(User, hash, password) != PasswordVerificationResult.Failed;
}
