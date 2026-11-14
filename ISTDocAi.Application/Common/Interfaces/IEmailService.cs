using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Common.Interfaces;

public interface IEmailService
{
    Task<Result<object>> SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
    Task<Result<object>> SendEmailAsync(IEnumerable<string> toEmails, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
