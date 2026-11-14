using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Common.Localization;

public interface IMessageLocalizer
{
    string Localize(string key, string fallback, params object[] args);
    Error Localize(Error error);
    Result Localize(Result result);
}
