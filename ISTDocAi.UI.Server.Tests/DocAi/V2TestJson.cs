using System.Net.Http.Json;
using System.Text.Json;
namespace ISTDocAi.UI.Server.Tests.DocAi;
internal static class V2TestJson
{
    public static async Task<T> ReadValue<T>(this HttpResponseMessage response)
    {
        var root = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        NUnit.Framework.Assert.That(root.GetProperty("IsSuccess").GetBoolean(), NUnit.Framework.Is.True);
        return root.GetProperty("Value").Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    public static async Task<T> GetValue<T>(this HttpClient client, string path) => await (await client.GetAsync(path)).ReadValue<T>();
}
