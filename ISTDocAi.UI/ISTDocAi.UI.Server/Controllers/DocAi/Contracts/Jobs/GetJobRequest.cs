namespace ISTDocAi.UI.Server.Controllers.DocAi.Contracts.Jobs;
// Descriptive contract only: raw query multiplicity and precedence are parsed by the controller.
public sealed record GetJobRequest(string Id, string? RunNo);
