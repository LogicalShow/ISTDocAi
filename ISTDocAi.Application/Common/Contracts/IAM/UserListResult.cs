namespace ISTDocAi.Application.Common.Contracts.IAM;

public record UserListResult(List<UserModel> Items, int TotalCount);
