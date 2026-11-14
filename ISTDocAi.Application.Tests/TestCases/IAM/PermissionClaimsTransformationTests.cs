using System.Security.Claims;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Infrastructure.IAM.Services;

namespace ISTDocAi.Application.Tests.TestCases.IAM;

[TestFixture]
public class PermissionClaimsTransformationTests
{
    [Test]
    public async Task TransformAsync_ReplacesTokenPermissionsWithCacheSnapshot()
    {
        var tokenIdentity = new ClaimsIdentity(
            [new Claim("uid", "user-1"), new Claim("p", "forged"), new Claim("permission", "FORGED.VIEW")],
            "Bearer");
        var principal = new ClaimsPrincipal(tokenIdentity);
        var cache = new StubPermissionCache(new PermissionSnapshot(true, true,
            new Dictionary<string, int>(), new HashSet<string> { "ADM.USR.VIEW" }, 1));

        var transformed = await new PermissionClaimsTransformation(cache).TransformAsync(principal);
        var permissions = transformed.FindAll("permission").Select(x => x.Value).ToArray();

        Assert.That(transformed.HasClaim("p", "forged"), Is.False);
        Assert.That(permissions, Is.EqualTo(new[] { "ADM.USR.VIEW" }));
        await new PermissionClaimsTransformation(cache).TransformAsync(transformed);
        Assert.That(transformed.FindAll("permission").Count(), Is.EqualTo(1));
    }

    private sealed class StubPermissionCache(PermissionSnapshot snapshot) : IPermissionCacheService
    {
        public Task<PermissionSnapshot> GetAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
        public void Invalidate(string userId) { }
    }
}
