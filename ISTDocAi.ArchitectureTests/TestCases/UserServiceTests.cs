using System.Security.Claims;
using ISTDocAi.Infrastructure.IAM.Services;
using Microsoft.AspNetCore.Http;

namespace ISTDocAi.ArchitectureTests.TestCases;

[TestFixture]
public class UserServiceTests
{
    [Test]
    public void UserId_ShouldReturnNull_WhenHttpContextIsUnavailable()
    {
        var service = new UserService(new HttpContextAccessor());

        Assert.That(service.UserId, Is.Null);
    }

    [Test]
    public void UserId_ShouldReturnNull_WhenAuthenticatedPrincipalHasNoIdentifier()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "TestAuthentication"))
        };
        var service = new UserService(new HttpContextAccessor { HttpContext = context });

        Assert.That(service.UserId, Is.Null);
    }

    [Test]
    public void UserId_ShouldReturnIdentifierClaim()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")],
            "TestAuthentication");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        var service = new UserService(new HttpContextAccessor { HttpContext = context });

        Assert.That(service.UserId, Is.EqualTo("user-123"));
    }
}
