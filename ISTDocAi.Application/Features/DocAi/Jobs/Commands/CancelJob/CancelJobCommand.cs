using ISTDocAi.Application.Common.Interfaces;
using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
namespace ISTDocAi.Application.Features.DocAi.Jobs.Commands.CancelJob;
public sealed record CancelJobCommand(Consumer Consumer, string RequestId) : ICommand<CancelJobResult>;
