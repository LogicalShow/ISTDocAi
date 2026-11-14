using ISTDocAi.Domain.Common.Enums;
using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.IAM;
using ISTDocAi.Application.Common.Interfaces.Serialization;
using ISTDocAi.Application.Common.Utilities;
using ISTDocAi.Domain.Common.Shared;

namespace ISTDocAi.Application.Common.Services;

public interface IActionLogService
{
    Task LogAction(
        Pub_ActionTypeEnum actionTypeId,
        string actionId,
        string? actionNo,
        object actionDetails,
        CancellationToken cancellationToken);

    Task SystemLogAction(
        Pub_ActionTypeEnum actionTypeId,
        string actionId,
        string? actionNo,
        object actionDetails,
       
        CancellationToken cancellationToken);
}

public class ActionLogService : IActionLogService
{
    private readonly IRepository<Pub_ActionLog> _repository;
    private readonly IUserService _userService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogPayloadSerializer _logPayloadSerializer;

    public ActionLogService(
        IRepository<Pub_ActionLog> actionLogRepository,
        IUserService userService,
        TimeProvider timeProvider,
        ILogPayloadSerializer logPayloadSerializer)
    {
        _repository = actionLogRepository;
        _userService = userService;
        _timeProvider = timeProvider;
        _logPayloadSerializer = logPayloadSerializer;
    }

    public async Task LogAction(
        Pub_ActionTypeEnum actionTypeId,
        string actionId,
        string? actionNo,
        object actionDetails,
        CancellationToken cancellationToken)
    {
        var actionLog = new Pub_ActionLog
        {
            Id = Extensions.GenerateEntityId(),
            UserID = _userService.UserId,
            ActionTypeID = Convert.ToString(actionTypeId),
            ActionID = actionId,
            ActionNo = actionNo,
            ActionTime = _timeProvider.GetLocalDateTime(),
            ActionDetails = _logPayloadSerializer.Serialize(actionDetails)
        };

        await _repository.AddAsync(actionLog, cancellationToken);
    }

    public async Task SystemLogAction(Pub_ActionTypeEnum actionTypeId, string actionId, string? actionNo, object actionDetails, CancellationToken cancellationToken)
    {
        var actionLog = new Pub_ActionLog
        {
            Id = Extensions.GenerateEntityId(),
            UserID = "system",
            ActionTypeID = Convert.ToString(actionTypeId),
            ActionID = actionId,
            ActionNo = actionNo,
            ActionTime = _timeProvider.GetLocalDateTime(),
            ActionDetails = _logPayloadSerializer.Serialize(actionDetails)
        };

        await _repository.AddAsync(actionLog, cancellationToken);
    }
}
