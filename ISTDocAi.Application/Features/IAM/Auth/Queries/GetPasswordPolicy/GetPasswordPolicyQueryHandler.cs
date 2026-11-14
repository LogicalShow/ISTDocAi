using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Features.IAM.Auth.Queries.GetPasswordPolicy;

public class GetPasswordPolicyQueryHandler(IUserPasswordService userPasswordService) : IQueryHandler<GetPasswordPolicyQuery, GetPasswordPolicyResponse>
{
    public Task<Result<GetPasswordPolicyResponse>> Handle(GetPasswordPolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = userPasswordService.GetPasswordPolicy();

        return Task.FromResult(Result<GetPasswordPolicyResponse>.Success(new GetPasswordPolicyResponse(
            policy.RequiredLength,
            policy.RequiredUniqueChars,
            policy.RequireNonAlphanumeric,
            policy.RequireLowercase,
            policy.RequireUppercase,
            policy.RequireDigit)));
    }
}
