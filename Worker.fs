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
open Microsoft.Extensions.Options

type WorkerOptions() =
    member val InterfaceName = Unchecked.defaultof<string> with get, set
    member val Domain = Unchecked.defaultof<string> with get, set
    member val RecordId = Unchecked.defaultof<string> with get, set
    member val ApiKey = Unchecked.defaultof<string> with get, set
    member val ZoneId = Unchecked.defaultof<string> with get, set
    member val Period = Unchecked.defaultof<TimeSpan> with get, set

type Worker(httpClient: HttpClient, options: IOptions<WorkerOptions>, logger: ILogger<Worker>) =
    inherit BackgroundService()

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
       httpClient.DefaultRequestHeaders.Authorization <- new AuthenticationHeaderValue("Bearer", options.Value.ApiKey)
    override _.ExecuteAsync(stoppingToken) =
        task{
            let mutable recordIp = ""

            use timer = new PeriodicTimer(options.Value.Period)
            while not stoppingToken.IsCancellationRequested do
                let allInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                match allInterfaces |> Seq.tryFind (fun i -> i.Name = options.Value.InterfaceName) with
                | Some networkInterface ->
                    let addresses =  getInterfaceAddresses networkInterface |> Enumerable.ToList
                    if addresses.Count = 0 then
                        logger.LogWarning("Network interface '{InterfaceName}' address not found.", options.Value.InterfaceName)
                    else
                        if addresses.Count > 1 then logger.LogWarning("Mulit addresses '{Addresses}'.", String.Join(", ", addresses))
                        let address = addresses.[0]
                        if address = recordIp then
                            logger.LogInformation("Same address.")
                        else
                            try
                                let request = new HttpRequestMessage(HttpMethod.Patch, $"zones/{options.Value.ZoneId}/dns_records/{options.Value.RecordId}")
                                request.Content <- new StringContent($"{{\"content\":\"{address}\"}}", System.Text.Encoding.UTF8, "application/json")
                                use! response = httpClient.SendAsync(request)
                                response.EnsureSuccessStatusCode() |> ignore
                
                                recordIp <- address
                                logger.LogInformation("Upload new address '{Address}' succeed.", address)
                            with
                            | e -> logger.LogError(e, "Exception occurred.")
                | None ->
                    logger.LogWarning("Network interface '{InterfaceName}' not found.", options.Value.InterfaceName);
                
                    (StringBuilder(), allInterfaces)
                    ||> Seq.fold (fun sb i -> sb.AppendFormat("{0}:{1}\r\n", i.Name, String.Join(", ", getInterfaceAddresses i)))
                    |> fun sb -> logger.LogInformation("All network interfaces:\r\n\r\n{AllNetworkInterfaces}", sb.ToString())
                do! timer.WaitForNextTickAsync(stoppingToken).AsTask() :> Task
        }
