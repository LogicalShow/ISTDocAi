using ISTDocAi.Application.Features.IAM.UserMenu.Queries.GetUserMenu;
using ISTDocAi.Domain.Common.Constants;
using ISTDocAi.Domain.Common.Shared.Response;
using ISTDocAi.Domain.Dtos.IAM;

namespace ISTDocAi.Application.Tests.TestCases.IAM;

[TestFixture]
public class GetUserMenuQueryHandlerTests
{
    [Test]
    public async Task Handle_ShouldReturnUnauthorized_WhenCurrentUserIdentityIsMissing()
    {
        var menuService = new TestUserMenuService();
        var handler = new GetUserMenuQueryHandler(
            new TestUserService { UserId = null },
            menuService);

        var result = await handler.Handle(new GetUserMenuQuery(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.AuthUserIdentityMissing));
            Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Unauthorized));
            Assert.That(menuService.RequestedUserId, Is.Null);
        });
    }

    [Test]
    public async Task Handle_ShouldLoadMenuForCurrentUser()
    {
        const string userId = "current-user-id";
        var expectedMenu = new UserMenuDto();
        var menuService = new TestUserMenuService(expectedMenu);
        var handler = new GetUserMenuQueryHandler(
            new TestUserService { UserId = userId },
            menuService);

        var result = await handler.Handle(new GetUserMenuQuery(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.SameAs(expectedMenu));
            Assert.That(menuService.RequestedUserId, Is.EqualTo(userId));
        });
    }

    [Test]
    public async Task Handle_ShouldPropagateMenuServiceFailure()
    {
        var expectedError = Error.NotFound(ErrorCodes.UserNotFound, "User not found.");
        var handler = new GetUserMenuQueryHandler(
            new TestUserService { UserId = "missing-user-id" },
            new TestUserMenuService(error: expectedError));

        var result = await handler.Handle(new GetUserMenuQuery(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(expectedError));
        });
    }

    private sealed class TestUserMenuService(
        UserMenuDto? menu = null,
        Error? error = null) : IUserMenuService
    {
        public string? RequestedUserId { get; private set; }

        public Task<Result<UserMenuDto>> GetUserMenuAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            RequestedUserId = userId;
            var result = error is null
                ? Result.Success(menu ?? new UserMenuDto())
                : Result.Failure<UserMenuDto>(error);

            return Task.FromResult(result);
        }
    }
}
