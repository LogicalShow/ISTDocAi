using System.Net;
using System.Text.Json;
using ISTDocAi.Application.Common.Interfaces.DocAi.Pricing;
using ISTDocAi.Application.Services.DocAi.Pricing;
namespace ISTDocAi.Infrastructure.Services.DocAi.Pricing;

public sealed class AzureRetailPriceClient(IHttpClientFactory clients, TimeProvider clock) : IAzureRetailPriceSource
{
    public const string ClientName = "AzureRetailPrices";
    internal const string Filter = "armRegionName eq 'francecentral' and (meterId eq '" + AzurePricePolicy.ExtractId + "' or meterId eq '" + AzurePricePolicy.ClassifyId + "')";
    internal static readonly Uri InitialUri = new("https://prices.azure.com:443/api/retail/prices?api-version=2023-01-01-preview&currencyCode=" + Uri.EscapeDataString("'USD'") + "&$filter=" + Uri.EscapeDataString(Filter));
    private const int PageBytes = 1024 * 1024;
    private const int CheckBytes = 4 * PageBytes;
    internal static Uri ValidateLink(string link)
    {
        if (link.Length > 8192 || !Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || uri.Host != "prices.azure.com" || uri.Port != 443 ||
            uri.AbsolutePath != "/api/retail/prices" || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidDataException("Invalid retail pagination URL.");
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in uri.Query.TrimStart('?').Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || !query.TryAdd(Uri.UnescapeDataString(parts[0].Replace("+", " ")), Uri.UnescapeDataString(parts[1].Replace("+", " "))))
                throw new InvalidDataException("Invalid retail pagination query.");
        }
        if (query.Keys.Any(k => k is not ("api-version" or "currencyCode" or "$filter" or "$skip")) ||
            query.GetValueOrDefault("api-version") != "2023-01-01-preview" ||
            query.GetValueOrDefault("currencyCode") != "'USD'" || query.GetValueOrDefault("$filter") != Filter ||
            (query.TryGetValue("$skip", out var skip) && (!int.TryParse(skip, out var count) || count < 0)))
            throw new InvalidDataException("Retail pagination changed the query.");
        return uri;
    }
    public async Task<IReadOnlyList<AzurePriceObservation>> ReadAsync(CancellationToken cancellationToken)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(120), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.Token);
        var ct = linked.Token;
        using var client = clients.CreateClient(ClientName);
        var rows = new List<AzurePriceObservation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Uri? next = InitialUri;
        var total = 0;
        for (var page = 0; next is not null; page++)
        {
            if (page >= 10) throw new InvalidDataException("Retail page limit exceeded.");
            next = ValidateLink(next.AbsoluteUri);
            // Normalize query order/escaping before repeat detection.
            var key = string.Join("&", next.Query.TrimStart('?').Split('&').Select(Uri.UnescapeDataString).Order(StringComparer.Ordinal));
            if (!seen.Add(key)) throw new InvalidDataException("Repeated retail page.");
            JsonDocument? parsed = null;
            for (var attempt = 0; ; attempt++)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
                using var requestToken = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
                TimeSpan? retryAfter = null;
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, next);
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken.Token);
                    if ((int)response.StatusCode is >= 300 and < 400) throw new InvalidDataException("Retail redirects are forbidden.");
                    var retryable = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
                        HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
                    if (retryable && attempt < 2)
                    {
                        retryAfter = response.Headers.RetryAfter?.Delta ??
                            (response.Headers.RetryAfter?.Date is { } date ? date - clock.GetUtcNow() : null);
                    }
                    else
                    {
                        if (!response.IsSuccessStatusCode) throw new InvalidDataException("Retail request refused.");
                        if (response.Content.Headers.ContentLength > PageBytes) throw new InvalidDataException("Retail page too large.");
                        await using var stream = await response.Content.ReadAsStreamAsync(requestToken.Token);
                        using var memory = new MemoryStream();
                        var buffer = new byte[8192];
                        int length;
                        while ((length = await stream.ReadAsync(buffer, requestToken.Token)) != 0)
                        {
                            total += length;
                            if (memory.Length + length > PageBytes || total > CheckBytes) throw new InvalidDataException("Retail byte limit exceeded.");
                            memory.Write(buffer, 0, length);
                        }
                        var payload = memory.ToArray();
                        // Parsing is within the request and total budgets; input and depth are bounded.
                        parsed = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 32 });
                        requestToken.Token.ThrowIfCancellationRequested();
                        break;
                    }
                }
                catch (HttpRequestException) when (attempt < 2 && !ct.IsCancellationRequested) { }
                catch (IOException) when (attempt < 2 && !ct.IsCancellationRequested) { }
                catch (OperationCanceledException)
                {
                    parsed?.Dispose(); parsed = null;
                    if (attempt >= 2 || ct.IsCancellationRequested) throw;
                }
                var delay = retryAfter ?? TimeSpan.FromMilliseconds((attempt == 0 ? 2000 : 5000) + Random.Shared.Next(0, 501));
                delay = TimeSpan.FromMilliseconds(Math.Clamp(delay.TotalMilliseconds, 0, 30000));
                await Task.Delay(delay, clock, ct);
            }
            using var json = parsed ?? throw new InvalidDataException("Retail document missing.");
            ct.ThrowIfCancellationRequested();
            var root = json.RootElement;
            if (!root.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Retail items missing.");
            foreach (var item in items.EnumerateArray())
            {
                if (rows.Count >= 2000 || item.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Retail item limit or shape invalid.");
                string? S(string name) => item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                decimal? D(string name) => item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : null;
                DateTimeOffset? date = item.TryGetProperty("effectiveStartDate", out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var d) ? d : null;
                rows.Add(new(S("meterId"), S("meterName"), S("currencyCode"), S("armRegionName"), S("unitOfMeasure"),
                    S("type"), D("tierMinimumUnits"), D("retailPrice"), date, S("productName"), S("skuName"), S("serviceName")));
            }
            next = root.TryGetProperty("NextPageLink", out var link) && link.ValueKind != JsonValueKind.Null
                ? link.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(link.GetString()) ? ValidateLink(link.GetString()!)
                    : throw new InvalidDataException("Invalid retail next page.") : null;
        }
        ct.ThrowIfCancellationRequested();
        return rows;
    }
}
