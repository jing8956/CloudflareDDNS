open System
open System.Net.Http
open System.Net.NetworkInformation
open System.Text.Json.Nodes
open System.Threading
open System.Linq
open System.Net.Http.Headers

let isWindows = OperatingSystem.IsWindows()
let getInterfaceAddresses (networkInterface: NetworkInterface) =
    networkInterface.GetIPProperties().UnicastAddresses
    |> Seq.where (fun i -> isWindows && i.DuplicateAddressDetectionState = DuplicateAddressDetectionState.Preferred)
    |> Seq.where (fun i -> i.Address.AddressFamily = System.Net.Sockets.AddressFamily.InterNetworkV6)
    |> Seq.where (fun i -> not i.Address.IsIPv6LinkLocal)
    |> Seq.where (fun i -> i.PrefixLength = 64)
    |> Seq.where (fun i -> isWindows && i.PrefixOrigin = PrefixOrigin.RouterAdvertisement)
    |> Seq.sortByDescending (fun i -> i.AddressPreferredLifetime)
    |> Seq.map (fun i -> i.Address.ToString())

match Environment.GetCommandLineArgs() with
| [| _; interfaceName; domain; apiKey; zoneId |] as args ->
    task {
        // warning IL3053: Assembly 'System.Net.Http' produced AOT analysis warnings. https://github.com/dotnet/runtime/issues/78367
        use httpClient = new HttpClient(BaseAddress = Uri("https://api.cloudflare.com/client/v4/"))
        httpClient.DefaultRequestHeaders.Authorization <- new AuthenticationHeaderValue("Bearer", apiKey)

        let! recordId, recordIp =
            task {
                use! body = httpClient.GetStreamAsync($"zones/{zoneId}/dns_records?name={domain}&type=AAAA")
                let content = JsonNode.Parse(body)
                let result = content.["result"].[0]
                let id = result.["id"].GetValue<string>()
                let content = result.["content"].GetValue<string>()
                return id, content
            }
        printfn "Record ID: %s" recordId
        printfn "Record IP: %s" recordIp
        let mutable recordIp = recordIp
        let period = args |> Array.tryItem 5 |> Option.defaultWith (fun() -> "00:01:00")
        use timer = new PeriodicTimer(TimeSpan.Parse(period))
        let mutable nextTick = true
        while nextTick do
            let result = 
                NetworkInterface.GetAllNetworkInterfaces()
                |> Seq.tryFind (fun i -> i.Name = interfaceName)
                |> Option.map (Ok)
                |> Option.defaultWith (fun() -> Error $"warning: network interface '{interfaceName}' not found.")
                |> Result.map (getInterfaceAddresses)
                |> Result.map (Enumerable.ToList)
                |> Result.bind (function
                    | addresses when addresses.Count = 0 -> Error $"warning: network interface '{interfaceName}' address not found."
                    | addresses when addresses.Count > 1 -> printfn $"""warning: mulit addresses '{String.Join(", ", addresses)}'."""; Ok(addresses)
                    | addresses -> Ok(addresses))
            match result with
            | Ok(addresses) when addresses.[0] = recordIp -> printfn "Same address."
            | Ok(addresses) ->
                let address = addresses.[0]
                try
                    let request = new HttpRequestMessage(HttpMethod.Patch, $"zones/{zoneId}/dns_records/{recordId}")
                    request.Content <- new StringContent($"{{\"content\":\"{address}\"}}", System.Text.Encoding.UTF8, "application/json")
                    use! response = httpClient.SendAsync(request)
                    response.EnsureSuccessStatusCode() |> ignore

                    recordIp <- address
                    printfn $"Upload new address '{address}' succeed."
                with
                | e -> printfn "%s" (e.ToString())
            | Error message -> printfn "%s" message
               
            let! next = timer.WaitForNextTickAsync()
            nextTick <- next
    } |> fun t -> t.GetAwaiter().GetResult()
| [| _; "-l" |] | [| _; "--list-interfaces" |] ->
    NetworkInterface.GetAllNetworkInterfaces()
    |> Seq.iter (fun i -> printfn $"""{i.Name}: {String.Join(", ", getInterfaceAddresses i)}""")
| _ ->
    printfn "CloudflareDDNS - periodically obtain the current IPv6 address from the NIC and update Cloudflare DNS record if changed."
    printfn ""
    printfn "usage: CloudflareDDNS -l | --list-interfaces"
    printfn "usage: CloudflareDDNS <interfaceName> <domain> <apiKey> <zoneId> [period]"
    printfn ""
    printfn "Options:"
    printfn "  -l, --list-interfaces list all network interfaces"
    printfn ""
    printfn "Parameters:"
    printfn "  <interfaceName> Required: network interfaces name, please run `CloudflareDDNS -l`."
    printfn "  <domain>        Required: upload DNS reqcord domain. eg: my-machine.example.com"
    printfn "  <apiKey>        Required: visit https://dash.cloudflare.com/profile/api-tokens to create a api key."
    printfn "  <zoneId>        Required: visit Cloudflare domain Overview page and find Zone ID."
    printfn "  [period]        Optional: period for check. default: 00:01:00"
