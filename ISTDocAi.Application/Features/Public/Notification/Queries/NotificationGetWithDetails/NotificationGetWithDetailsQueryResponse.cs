using ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetWithDetails.Contracts;

namespace ISTDocAi.Application.Features.Public.Notification.Queries.NotificationGetWithDetails;

public class NotificationGetWithDetailsQueryResponse
{
    public string Id { get; set; }

    public string Name { get; set; }

   public ICollection<User> SelectedUsers { get; set; }

   public ICollection<UserLookup> UnSelectedUsers { get; set; }

}
