namespace CloudflareDDNS

open System
open System.Net.Http
open System.Net.Http.Headers
open System.Text.Json.Nodes

type CloudflareClient(httpClient: HttpClient) =

    do httpClient.BaseAddress <- "https://api.cloudflare.com/client/v4/" |> Uri
    member _.ApiKey with set(v:string) = httpClient.DefaultRequestHeaders.Authorization <- new AuthenticationHeaderValue("Bearer", v)

    member _.FindRecordsAsync(zoneId: string, domain: string) =
        task {
            let! bodyString = httpClient.GetStringAsync($"zones/{zoneId}/dns_records?name={domain}&type=AAAA")
            let body = JsonNode.Parse(bodyString)
            return body.["result"].AsArray()
            |> Seq.map (fun item ->
                struct {|
                    id = item.["id"].GetValue<string>()
                    content = item.["content"].GetValue<string>()
                |})
        }

    member _.SetAddress(zoneId: string, recordId: string, address: string) =
        task {
            let request = new HttpRequestMessage(HttpMethod.Patch, $"zones/{zoneId}/dns_records/{recordId}")
            request.Content <- new StringContent($"{{\"content\":\"{address}\"}}", System.Text.Encoding.UTF8, "application/json")
            use! response = httpClient.SendAsync(request)
            response.EnsureSuccessStatusCode() |> ignore
        } :> System.Threading.Tasks.Task