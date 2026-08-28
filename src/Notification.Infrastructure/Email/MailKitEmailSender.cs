using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Notification.Application.Abstractions.Email;
using Notification.Application.Abstractions.Security;
using Notification.Application.Senders;
using Notification.Infrastructure.Configuration;

namespace Notification.Infrastructure.Email;

public sealed class MailKitEmailSender(ISecretCipher cipher, IOptions<SmtpOptions> options) : IEmailSender
{
    public Task<string?> SendAsync(ResolvedSender sender, string recipientEmail, string subject, string body,
        CancellationToken ct) => SendAsync(sender, recipientEmail, subject, body, null, ct);

    public async Task SendTestAsync(ResolvedSender sender, string recipientEmail, DateTimeOffset now, CancellationToken ct)
    {
        await SendAsync(sender, recipientEmail, $"[notification-server] SMTP test: {sender.Key}",
            $"SMTP configuration '{sender.Key}' was tested at {now:O}.", null, ct);
    }

    public async Task<string?> SendAsync(ResolvedSender sender, string recipientEmail, string subject, string? textBody,
        string? htmlBody, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(options.Value.TimeoutMs);
        using var client = new SmtpClient { Timeout = options.Value.TimeoutMs };
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(sender.FromName, sender.FromEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;
        message.Body = (textBody, htmlBody) switch
        {
            (not null, not null) => new Multipart("alternative")
            {
                new TextPart("plain") { Text = textBody },
                new TextPart("html") { Text = htmlBody }
            },
            (not null, null) => new TextPart("plain") { Text = textBody },
            (null, not null) => new TextPart("html") { Text = htmlBody },
            _ => throw new ArgumentException("At least one email body is required.")
        };

        try
        {
            var socketOptions = sender.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : (sender.Port == 587 ? SecureSocketOptions.StartTls : (sender.Secure ? SecureSocketOptions.Auto : SecureSocketOptions.StartTlsWhenAvailable));
            await client.ConnectAsync(sender.Host, sender.Port, socketOptions, timeout.Token);
            var password = cipher.Decrypt(sender.PasswordEncrypted, sender.TenantId, sender.Id);
            await client.AuthenticateAsync(sender.Username, password, timeout.Token);
            var providerMessageId = await client.SendAsync(message, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
            return providerMessageId;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new EmailSendException("SMTP_TIMEOUT", true);
        }
        catch (MailKit.Security.AuthenticationException)
        {
            throw new EmailSendException("SMTP_AUTHENTICATION", false);
        }
        catch (SslHandshakeException)
        {
            throw new EmailSendException("SMTP_TLS", false);
        }
        catch (NotSupportedException)
        {
            throw new EmailSendException("SMTP_TLS", false);
        }
        catch (SocketException exception)
        {
            var code = exception.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData ? "SMTP_DNS" : "SMTP_CONNECTION";
            throw new EmailSendException(code, true);
        }
        catch (SmtpCommandException exception) when ((int)exception.StatusCode is >= 400 and < 500)
        {
            throw new EmailSendException("SMTP_TRANSIENT", true);
        }
        catch (SmtpCommandException exception)
        {
            var code = exception.ErrorCode == SmtpErrorCode.RecipientNotAccepted ? "RECIPIENT_REJECTED" : "SMTP_PROVIDER";
            throw new EmailSendException(code, false);
        }
        catch (MailKit.ServiceNotAuthenticatedException)
        {
            throw new EmailSendException("SMTP_AUTHENTICATION", false);
        }
        catch (IOException)
        {
            throw new EmailSendException("SMTP_CONNECTION", true);
        }
        catch (SmtpProtocolException)
        {
            throw new EmailSendException("SMTP_PROVIDER", false);
        }
    }
}
