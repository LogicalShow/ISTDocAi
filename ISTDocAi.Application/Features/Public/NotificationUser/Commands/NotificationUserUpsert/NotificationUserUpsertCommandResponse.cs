using ISTDocAi.Domain.Dtos.Shared.Deletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISTDocAi.Application.Features.Public.NotificationUser.Commands.NotificationUserUpsert
{
    public record NotificationUserUpsertCommandResponse(string Message,
    IReadOnlyList<string> DeletedIds,
    IReadOnlyList<BlockedDeleteItem> Blocked
) : DeleteCommandResponse(Message, DeletedIds, Blocked);

}
