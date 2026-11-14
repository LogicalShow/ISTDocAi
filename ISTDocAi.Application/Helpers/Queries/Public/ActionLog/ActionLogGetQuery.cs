using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Collections.Generic;
using MediatR;
using ISTDocAi.Domain.Entities.Public;
using ISTDocAi.Application.Common.Interfaces;

namespace ISTDocAi.Application.Helpers.Queries.Public.ActionLog;

public record ActionLogGetQuery : IRequest<Pub_ActionLog?>
{
    public string Id { get; set; }
}

public class ActionLogGetQueryHandler : IRequestHandler<ActionLogGetQuery, Pub_ActionLog?>
{
    private readonly IRepository<Pub_ActionLog> _repository;

    public ActionLogGetQueryHandler(IRepository<Pub_ActionLog> repository)
    {
        _repository = repository;
    }

    public async Task<Pub_ActionLog?> Handle(ActionLogGetQuery request, CancellationToken cancellationToken)
    {
        // get
        var actionLog = await _repository.GetAsync(
                            request.Id,
                            [(x => x.Pub_ActionType)],
                            cancellationToken);
        // map if needed
        //

        return actionLog;
    }
}
