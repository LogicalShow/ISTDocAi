using ISTDocAi.Application.Common.Contracts.IAM;

namespace ISTDocAi.UI.Server.Controllers.IAM.Contracts.Roles;

public class RoleDeleteRequest
{
    public List<RoleDeleteRequestItem> Items { get; set; } = new();

    public List<RoleDeleteItem> ToDeleteItems() =>
        Items.Select(i => new RoleDeleteItem(i.Id, i.ConcurrencyStamp)).ToList();
}
