using ISTDocAi.Application.Common.Contracts.UserQuery;
using ISTDocAi.Domain.Common.Enums;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using ISTDocAi.Infrastructure.IAM.Models;

namespace ISTDocAi.Infrastructure.Data.Repositories.IAM;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<List<IAM_User>> GetAllUsersAsync(CancellationToken cancellationToken)
    {
        var users = context.Users.AsNoTracking();

        var result = await users.ToListAsync(cancellationToken);

        return result;
    }

    public async Task<List<IAM_User>> SearchUsersAsync(string? term,int pageNo,int pageRowCount, CancellationToken cancellationToken)
    {
        var users = await context.Users
            .AsNoTracking()
            .Where(u => (string.IsNullOrEmpty(term) || u.UserName.Contains(term))
            && (string.IsNullOrEmpty(term) || u.FullName.Contains(term))
            && (string.IsNullOrEmpty(term) || u.Email.Contains(term)))
            .Skip((pageNo - 1) * pageRowCount)
            .Take(pageRowCount)
           .ToListAsync(cancellationToken);
        return users;
    }

    public async Task<IEnumerable<string>> GetUserEmailsByNotificationId(string notificationId, CancellationToken ct)
    {
        var userIds = await context.Pub_NotificationUsers
            .AsNoTracking()
            .Where(nu => nu.NotificationId == notificationId && nu.IsAssigned && nu.SendingWay != SendingWay.System)
            .Select(nu => nu.UserId).ToListAsync();
        if (userIds == null || !userIds.Any())
        {
            return Enumerable.Empty<string>();
        }
        var emails = await context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => u.Email)
            .Where(email => email != null)
            .ToListAsync();

        return emails!;
    }

    public async Task<string?> GetUserNameById(string userId, CancellationToken cancellationToken)
    {
        var userName = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync(cancellationToken);
        return userName ;
    }

    public async Task<Dictionary<string, string>> GetFullNamesByIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList() ?? new List<string>();
        if (ids.Count == 0)
            return new Dictionary<string, string>();

        var users = await context.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(u => u.Id, u => u.FullName ?? string.Empty);
    }
    public async Task<UserLookupModel?> GetUserByEmailAsync(
string email,
CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => new UserLookupModel(
                u.Id,
                u.UserName))
            .FirstOrDefaultAsync(cancellationToken);
    }
    public async Task<string?> GetUserEmailByIdAsync(
    string userId,
    CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
