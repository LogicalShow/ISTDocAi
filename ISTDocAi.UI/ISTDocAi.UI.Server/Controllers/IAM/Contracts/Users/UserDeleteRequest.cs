using ISTDocAi.Application.Common.Contracts.IAM;

namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Users;

public class UserDeleteRequest
{
    public List<UserDeleteRequestItem> Items { get; set; } = new();

    public List<UserDeleteItem> ToDeleteItems() =>
        Items.Select(i => new UserDeleteItem(i.Id, i.ConcurrencyStamp)).ToList();
}
