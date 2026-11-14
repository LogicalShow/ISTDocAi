namespace ISTDocAi.UI.Server.Controllers.DocAi.Contracts.Usage;
// Descriptive contract only: raw query multiplicity and precedence are parsed by the controller.
public sealed record GetCycleDocumentsRequest(string GrantId, string? Limit, string? AfterChargeId);
