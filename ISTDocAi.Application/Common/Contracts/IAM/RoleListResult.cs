namespace ISTDocAi.Application.Common.Contracts.IAM;

public record RoleListResult(List<RoleModel> Items, int TotalCount);
