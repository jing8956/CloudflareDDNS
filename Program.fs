namespace CloudflareDDNS

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open System.Net.Http.Json
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Options
open System.Net.Http
open System.Text.Json.Nodes
open System.Net.Http.Headers

module Program =
    let createHostBuilder args =
        Host.CreateDefaultBuilder(args)
            .UseSystemd()
            .UseWindowsService(fun o -> o.ServiceName <- "Cloudflare DDNS")
            .ConfigureServices(fun hostContext services ->
                services.Configure<WorkerOptions>(hostContext.Configuration) |> ignore
                services.AddHttpClient() |> ignore
                services.AddHostedService<Worker>() |> ignore)

    let command (host: IHost) =
        let options = host.Services.GetRequiredService<IOptions<WorkerOptions>>()
        let options = options.Value
        let domain = options.Domain
        if String.IsNullOrEmpty(domain) then false else

        let zoneId = options.ZoneId
        use httpClient = new HttpClient()
        httpClient.BaseAddress <- "https://api.cloudflare.com/client/v4/" |> Uri
        httpClient.DefaultRequestHeaders.Authorization <- new AuthenticationHeaderValue("Bearer", options.ApiKey)
        let content = httpClient.GetFromJsonAsync<JsonNode>($"zones/{zoneId}/dns_records?name={domain}&type=AAAA").Result
        let results = content.["result"].AsArray()
        printfn "%s\t\t\t\t%s" "RecordId" "IP Address"
        for result in results do
            let id = result.["id"].GetValue<string>()
            let content = result.["content"].GetValue<string>()
            printfn "%s\t%s" id content
        true

    [<EntryPoint>]
    let main args =
        let host = createHostBuilder(args).Build()
        if command host |> not then host.Run()
        0
        // if args.Length > 1 && args.[0] = "--search" then
        //     let domain 
        //     use httpClient = new System.Net.Http.HttpClient()
        //     let content = httpClient.
        // else
        //     createHostBuilder(args).Build().Run()
        //     0 // exit code
