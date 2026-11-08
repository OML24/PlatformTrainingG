using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net;
using ZuLms.Domain.Entities.Notify;

namespace ZuLms.Infrastructure.Email;

public enum EmailSendOutcome { Sent, Retry, Failed, Uncertain }

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task<(EmailSendOutcome Outcome, string? Error)> SendAsync(EmailOutbox message,
        string fromAddress, string fromName, CancellationToken cancellationToken);
}

public sealed class MailKitEmailSender : IEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZU_SMTP_HOST")) &&
        int.TryParse(Environment.GetEnvironmentVariable("ZU_SMTP_PORT"), out int port) && port is > 0 and <= 65535;

    public async Task<(EmailSendOutcome Outcome, string? Error)> SendAsync(EmailOutbox message,
        string fromAddress, string fromName, CancellationToken cancellationToken)
    {
        string host = Environment.GetEnvironmentVariable("ZU_SMTP_HOST")
            ?? throw new InvalidOperationException("ZU_SMTP_HOST is required.");
        if (!int.TryParse(Environment.GetEnvironmentVariable("ZU_SMTP_PORT"), out int port))
            throw new InvalidOperationException("ZU_SMTP_PORT is required.");
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(fromName, fromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.ToAddress, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.BodyHtml }.ToMessageBody();
        using var client = new SmtpClient();
        try
        {
            bool ssl = bool.TryParse(Environment.GetEnvironmentVariable("ZU_SMTP_SSL"), out bool value) && value;
            string? user = Environment.GetEnvironmentVariable("ZU_SMTP_USER");
            string? password = Environment.GetEnvironmentVariable("ZU_SMTP_PASSWORD");
            bool allowLoopbackPlaintext = bool.TryParse(
                Environment.GetEnvironmentVariable("ZU_SMTP_ALLOW_LOOPBACK_PLAINTEXT"), out bool plain) && plain;
            SecureSocketOptions socketOptions = ssl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            if (allowLoopbackPlaintext)
            {
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                if (addresses.Length == 0 || addresses.Any(x => !IPAddress.IsLoopback(x)) ||
                    !string.IsNullOrWhiteSpace(user) || !string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException("Loopback plaintext SMTP requires a loopback-only host and no credentials.");
                socketOptions = SecureSocketOptions.None;
            }
            await client.ConnectAsync(host, port, socketOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(user))
                await client.AuthenticateAsync(user, password ?? string.Empty, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or TimeoutException or SmtpCommandException or SmtpProtocolException or System.Security.Authentication.AuthenticationException)
        {
            return (EmailSendOutcome.Retry, ex.GetType().Name);
        }

        try
        {
            await client.SendAsync(mime, cancellationToken);
            return (EmailSendOutcome.Sent, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (SmtpCommandException ex)
        {
            return ((int)ex.StatusCode >= 500 ? EmailSendOutcome.Failed : EmailSendOutcome.Retry,
                $"SmtpStatus:{(int)ex.StatusCode}");
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or SmtpProtocolException or OperationCanceledException)
        {
            return (EmailSendOutcome.Uncertain, ex.GetType().Name);
        }
        finally
        {
            if (client.IsConnected)
            {
                try { await client.DisconnectAsync(true, CancellationToken.None); }
                catch (Exception) { /* Delivery result is already classified. */ }
            }
        }
    }
}
