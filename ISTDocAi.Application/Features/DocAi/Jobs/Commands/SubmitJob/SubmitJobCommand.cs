using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using ISTDocAi.Application.Common.Interfaces;
namespace ISTDocAi.Application.Features.DocAi.Jobs.Commands.SubmitJob;
public sealed record SubmitJobCommand(Consumer Consumer, PreparedSubmission File) : ICommand<SubmitJobCommandResponse>;
