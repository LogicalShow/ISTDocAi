using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Domain.Dtos.IAM;

namespace ISTDocAi.Application.Features.IAM.UserMenu.Queries.GetUserMenu;

public sealed record GetUserMenuQuery : IQuery<UserMenuDto>;
