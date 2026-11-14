using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Services.Public.SettingService;
using ISTDocAi.Domain.Common.Shared.Response;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using ISTDocAi.Domain.Common.Constants;

namespace ISTDocAi.Infrastructure.Services;

public class EmailService(ISettingService settingService, ILogger<EmailService> logger) : IEmailService
{
    public async Task<Result<object>> SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var body = BuildEmailBody(htmlBody);
        return await SendEmailCoreAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task<Result<object>> SendEmailAsync(IEnumerable<string> toEmails, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var body = BuildEmailBody(htmlBody);
        return await SendEmailCoreAsync(toEmails, subject, body, cancellationToken);
    }

    private async Task<Result<object>> SendEmailCoreAsync(string toEmail, string subject, MimeEntity body, CancellationToken cancellationToken = default)
    {
        return await SendEmailCoreAsync(new[] { toEmail }, subject, body, cancellationToken);
    }

    private async Task<Result<object>> SendEmailCoreAsync(
    IEnumerable<string> toEmails,
    string subject,
    MimeEntity body,
    CancellationToken cancellationToken = default)
    {
        var emailConfigResult = await settingService.GetEmailConfigurationAsync(cancellationToken);
        if (!emailConfigResult.IsSuccess)
        {
            logger.LogError("Failed to load email configuration: {Error}", emailConfigResult.Error);
            return Result<object>.Failure(emailConfigResult.Error);
        }

        var emailConfig = emailConfigResult.Value;

        using var smtp = new SmtpClient();

        try
        {
            await smtp.ConnectAsync(
                emailConfig.EmailHost,
                emailConfig.EmailPort,
                emailConfig.EmailEnableSSL ? SecureSocketOptions.StartTls  : SecureSocketOptions.None,
                cancellationToken);

            await smtp.AuthenticateAsync(
                emailConfig.EmailUsername,
                emailConfig.DecryptedPassword,
                cancellationToken);

            var sentCount = 0;
            var failedEmails = new List<string>();

            foreach (var email in toEmails)
            {
                try
                {
                    var message = new MimeMessage();
                    message.From.Add(MailboxAddress.Parse(emailConfig.SenderEmail));
                    message.To.Add(MailboxAddress.Parse(email));
                    message.Subject = subject;
                    message.Body = body;

                    await smtp.SendAsync(message, cancellationToken);
                    sentCount++;
                    logger.LogInformation("Email sent successfully to {Email}", email);
                }
                catch (Exception ex)
                {
                    failedEmails.Add(email);
                    logger.LogError(ex, "Failed to send email to {Email}", email);
                }
            }

            await smtp.DisconnectAsync(true, cancellationToken);

            if (failedEmails.Count == 0)
                return Result.Success("Notification emails processed");

            if (sentCount == 0)
            {
                logger.LogError("Failed to send all notification emails to: {FailedEmails}",
                    string.Join(", ", failedEmails));
                return Result<object>.Failure(
                    Error.Unexpected(ErrorCodes.EmailSendFailed, "Failed to send notification emails"));
            }

            logger.LogError("Failed to send notification emails to some recipients: {FailedEmails}",
                string.Join(", ", failedEmails));
            return Result<object>.Failure(
                Error.Unexpected(ErrorCodes.EmailPartialSendFailed,
                    $"Failed to send notification emails to: {string.Join(", ", failedEmails)}"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send notification emails");
            return Result<object>.Failure(
                Error.Unexpected(ErrorCodes.EmailSendFailed, "Failed to send notification emails"));
        }
    }

    private MimeEntity BuildEmailBody(string htmlBody)
    {
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = htmlBody // Fallback for email clients that don't support HTML
        };
        return bodyBuilder.ToMessageBody();
    }
}
