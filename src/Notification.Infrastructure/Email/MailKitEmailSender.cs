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
    public async Task SendTestAsync(ResolvedSender sender, string recipientEmail, DateTimeOffset now, CancellationToken ct)
    {
        await SendAsync(sender, recipientEmail, $"[notification-server] SMTP test: {sender.Key}", $"SMTP configuration '{sender.Key}' was tested at {now:O}.", ct);
    }

    public async Task<string?> SendAsync(ResolvedSender sender, string recipientEmail, string subject, string body, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(options.Value.TimeoutMs);
        using var client = new SmtpClient { Timeout = options.Value.TimeoutMs };
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(sender.FromName, sender.FromEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        try
        {
            var socketOptions = sender.Secure ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(sender.Host, sender.Port, socketOptions, timeout.Token);
            var password = cipher.Decrypt(sender.PasswordEncrypted, sender.TenantId, sender.Id);
            await client.AuthenticateAsync(sender.Username, password, timeout.Token);
            var providerMessageId = await client.SendAsync(message, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
            return providerMessageId;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new EmailSendException("timeout", true);
        }
        catch (MailKit.Security.AuthenticationException)
        {
            throw new EmailSendException("authentication");
        }
        catch (SslHandshakeException)
        {
            throw new EmailSendException("tls_handshake");
        }
        catch (NotSupportedException)
        {
            throw new EmailSendException("tls_not_supported");
        }
        catch (SocketException exception)
        {
            throw new EmailSendException(exception.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData ? "dns" : "connection");
        }
        catch (SmtpCommandException exception) when (exception.ErrorCode == SmtpErrorCode.RecipientNotAccepted)
        {
            throw new EmailSendException("recipient_rejected");
        }
        catch (SmtpCommandException)
        {
            throw new EmailSendException("provider");
        }
        catch (MailKit.ServiceNotAuthenticatedException)
        {
            throw new EmailSendException("authentication");
        }
        catch (IOException)
        {
            throw new EmailSendException("connection");
        }
        catch (SmtpProtocolException)
        {
            throw new EmailSendException("provider");
        }
    }
}
