using ISTDocAi.Application.Common.Interfaces.DocAi.Persistence;
using ISTDocAi.Application.Common.Interfaces.DocAi.Storage;
using ISTDocAi.Application.Common.Contracts.DocAi.V1;
using ISTDocAi.Application.Common.Interfaces.DocAi.Intake;
using System.Text.Json;
namespace ISTDocAi.Application.Services.DocAi.Intake;
public sealed class GetCatalog(ICatalogReader catalog)
{
    public async Task<CatalogResponse> ExecuteAsync(Consumer consumer, CancellationToken ct)
    {
        var models = await catalog.ReadAsync(ct);
        return new(consumer.Code, models.Where(m => m.Kind == "extract" && m.DocumentTypeCode != "AUTO").Select(m => new CatalogItem(m.DocumentTypeCode, m.ShippingLineCode, (double)m.MinFieldConfidence)).ToArray(),
            models.Where(m => m.ShippingLineCode is null && (m.Kind == "classify" || m.DocumentTypeCode != "AUTO" && m.Kind == "extract"))
                .Select(m => m.DocumentTypeCode).Distinct().Select(t => new AutoDetectItem(t)).ToArray());
    }
}
