using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Features.IAM.Users.Queries.GetUser;

public record GetUserQuery(string Id) : IQuery<GetUserResponse>;
