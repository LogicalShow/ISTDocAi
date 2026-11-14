using ISTDocAi.Application.Features.Public.NotificationUser.Commands.NotificationUserUpsert.Contracts;
using ISTDocAi.Domain.Entities.Public;
using Mapster;

namespace ISTDocAi.Application.Common.Mappings.Mapster.Public;

public sealed class NotificationUserMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<SelectedNotificationUser, AddedNotificationUser>();
        config.NewConfig<SelectedNotificationUser, UpdatedNotificationUser>();
        config.NewConfig<SelectedNotificationUser, DeletedNotificationUser>();

        config.NewConfig<AddedNotificationUser, Pub_NotificationUser>()
            .Map(destination => destination.SendingWay, source => source.SendingWayId)
            .Ignore(destination => destination.NotificationId);

        config.NewConfig<UpdatedNotificationUser, Pub_NotificationUser>()
            .Map(destination => destination.SendingWay, source => source.SendingWayId);
    }
}
