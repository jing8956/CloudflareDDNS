namespace CloudflareDDNS

open System
open System.Linq
open System.Net.Http
open System.Net.Http.Headers
open System.Net.Http.Json
open System.Net.NetworkInformation
open System.Text
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type Worker(httpClient: HttpClient, configuration: IConfiguration, logger: ILogger<Worker>, host: IHost) =
    inherit BackgroundService()
    let interfaceName = configuration.GetValue<string>("InterfaceName")
    let domain = configuration.GetValue<string>("Domain")   
    let apiKey = configuration.GetValue<string>("ApiKey")
    let zoneId = configuration.GetValue<string>("ZoneId")
    let period = configuration.GetValue<TimeSpan>("Period")

    let isNotWindows = OperatingSystem.IsWindows() |> not
    let getInterfaceAddresses (networkInterface: NetworkInterface) =
        networkInterface.GetIPProperties().UnicastAddresses
        |> Seq.where (fun i -> isNotWindows || i.DuplicateAddressDetectionState = DuplicateAddressDetectionState.Preferred)
        |> Seq.where (fun i -> i.Address.AddressFamily = System.Net.Sockets.AddressFamily.InterNetworkV6)
        |> Seq.where (fun i -> not i.Address.IsIPv6LinkLocal)
        |> Seq.where (fun i -> i.PrefixLength = 64)
        |> Seq.where (fun i -> isNotWindows || i.PrefixOrigin = PrefixOrigin.RouterAdvertisement)
        |> Seq.sortByDescending (fun i -> i.AddressPreferredLifetime)
        |> Seq.map (fun i -> i.Address.ToString())

    do httpClient.BaseAddress <- "https://api.cloudflare.com/client/v4/" |> Uri
       httpClient.DefaultRequestHeaders.Authorization <- new AuthenticationHeaderValue("Bearer", apiKey)
    override _.ExecuteAsync(stoppingToken) =
        task{
            let allInterfaces = NetworkInterface.GetAllNetworkInterfaces()
            match allInterfaces |> Seq.tryFind (fun i -> i.Name = interfaceName) with
            | Some networkInterface ->
                let! recordId, recordIp = 
                    task {
                        let! content = httpClient.GetFromJsonAsync<JsonNode>($"zones/{zoneId}/dns_records?name={domain}&type=AAAA", stoppingToken)
                        let result = content.["result"].[0]
                        let id = result.["id"].GetValue<string>()
                        let content = result.["content"].GetValue<string>()
                        return id, content
                    }
                logger.LogInformation("Record ID: {RecordID}", recordId)
                logger.LogInformation("Record IP: {RecordIP}", recordIp)
                let mutable recordIp = recordIp
                use timer = new PeriodicTimer(period)
                while not stoppingToken.IsCancellationRequested do
                    let addresses =  getInterfaceAddresses networkInterface |> Enumerable.ToList
                    if addresses.Count = 0 then
                        logger.LogWarning("Network interface '{InterfaceName}' address not found.", interfaceName)
                    else
                        if addresses.Count > 1 then logger.LogWarning("Mulit addresses '{Addresses}'.", String.Join(", ", addresses))
                        let address = addresses.[0]
                        if address = recordIp then
                            logger.LogInformation("Same address.")
                        else
                            try
                                let request = new HttpRequestMessage(HttpMethod.Patch, $"zones/{zoneId}/dns_records/{recordId}")
                                request.Content <- new StringContent($"{{\"content\":\"{address}\"}}", System.Text.Encoding.UTF8, "application/json")
                                use! response = httpClient.SendAsync(request)
                                response.EnsureSuccessStatusCode() |> ignore

                                recordIp <- address
                                logger.LogInformation("Upload new address '{Address}' succeed.", address)
                            with
                            | e -> logger.LogError(e, "Exception occurred.")
                    do! timer.WaitForNextTickAsync(stoppingToken).AsTask() :> Task
            | None ->
                logger.LogWarning("Network interface '{InterfaceName}' not found.", interfaceName);

                (StringBuilder(), allInterfaces)
                ||> Seq.fold (fun sb i -> sb.AppendFormat("{0}:{1}\r\n", i.Name, String.Join(", ", getInterfaceAddresses i)))
                |> fun sb -> logger.LogInformation("All network interfaces:\r\n\r\n{AllNetworkInterfaces}", sb.ToString())
                do! host.StopAsync(stoppingToken)
        }
