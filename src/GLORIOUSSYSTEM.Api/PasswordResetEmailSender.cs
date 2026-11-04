using System.Net;
using System.Net.Mail;

namespace GLORIOUSSYSTEM.Api;

public interface IPasswordResetEmailSender
{
    bool IsConfigured { get; }

    Task SendCodeAsync(
        string recipientEmail,
        string recipientName,
        string code,
        CancellationToken cancellationToken);
}

public sealed class GmailPasswordResetEmailSender : IPasswordResetEmailSender
{
    readonly string _host;
    readonly int _port;
    readonly string _username;
    readonly string _password;
    readonly string _fromAddress;
    readonly string _fromName;

    public GmailPasswordResetEmailSender(IConfiguration configuration)
    {
        _host = configuration["Email:SmtpHost"]?.Trim() ?? "smtp.gmail.com";
        _port = configuration.GetValue("Email:SmtpPort", 587);
        _username = configuration["Email:SmtpUser"]?.Trim() ?? string.Empty;
        _password = configuration["Email:SmtpPassword"]?.Trim() ?? string.Empty;
        _fromAddress = configuration["Email:FromAddress"]?.Trim() ?? _username;
        _fromName = configuration["Email:FromName"]?.Trim() ?? "GLORIOUS SYSTEM";
    }

    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(_host)
            && _port is > 0 and <= 65535
            && !string.IsNullOrWhiteSpace(_username)
            && !string.IsNullOrWhiteSpace(_password)
            && MailAddress.TryCreate(_fromAddress, out _);

    public async Task SendCodeAsync(
        string recipientEmail,
        string recipientName,
        string code,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Email:SmtpUser and Email:SmtpPassword must be configured before sending password reset emails.");

        using var message = new MailMessage
        {
            From = new MailAddress(_fromAddress, _fromName),
            Subject = "Your GLORIOUS SYSTEM password reset code",
            Body = $"Hello {recipientName},\n\nYour GLORIOUS SYSTEM password reset code is:\n\n{code}\n\nThis code expires in 15 minutes and can only be used once. If you did not request a password reset, you can ignore this message.\n\nGLORIOUS SYSTEM",
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipientEmail));

        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_username, _password),
            Timeout = 15000
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
