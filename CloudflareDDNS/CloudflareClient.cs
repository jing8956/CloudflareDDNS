using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CloudflareDDNS;

public partial class CloudflareClient(HttpClient httpClient)
{
    public record struct RecordItem(string Id, string Content);

    public async Task<RecordItem[]> FindRecordsAsync(string domain)
    {
        using var responseStream = await httpClient.GetStreamAsync($"dns_records?name={domain}&type=AAAA");
        var body = await JsonNode.ParseAsync(responseStream);
        var result = body!["result"]!.AsArray();

        return result.Deserialize(CloudflareClientJsonSerializerContext.Default.RecordItemArray)!;
    }

    public record struct SetAddressContent(string Content);

    public async Task SetAddress(string recordId, string address)
    {
        using var response = await httpClient.PatchAsJsonAsync($"dns_records/{recordId}",
            new SetAddressContent(address), CloudflareClientJsonSerializerContext.Default.SetAddressContent);
        response.EnsureSuccessStatusCode();
    }

    [JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
    [JsonSerializable(typeof(RecordItem[]))]
    [JsonSerializable(typeof(SetAddressContent))]
    internal partial class CloudflareClientJsonSerializerContext : JsonSerializerContext
    {

    }
}


