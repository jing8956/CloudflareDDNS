namespace CloudflareDDNS

open System
open System.Net.Http
open System.Threading

#if !AOT
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Options
open Microsoft.Extensions.Configuration
open System.Reflection
#endif

module Program =

    let exitCode = 0

    let command (options: WorkerOptions) =
        let domain = options.Domain
        if String.IsNullOrEmpty(domain) then false else
        let zoneId = options.ZoneId

        use httpClient = new HttpClient()
        let client = CloudflareClient(httpClient, ApiKey = options.ApiKey)
        printfn "%s\t\t\t\t%s" "RecordId" "IP Address"
        client.FindRecordsAsync(zoneId, domain).Result
        |> Seq.iter(fun result -> printfn "%s\t%s" result.id result.content)

        true

#if AOT
    [<EntryPoint>]
    let main args =
        let options = new WorkerOptions(
            InterfaceName = Environment.GetEnvironmentVariable("InterfaceName"),
            Domain = Environment.GetEnvironmentVariable("Domain"),
            RecordId = Environment.GetEnvironmentVariable("RecordId"),
            ApiKey = Environment.GetEnvironmentVariable("ApiKey"),
            ZoneId = Environment.GetEnvironmentVariable("ZoneId"),
            Period = TimeSpan.Parse(Environment.GetEnvironmentVariable("Period")))
        if command options |> not then
            use cts = new CancellationTokenSource()
            use httpClient = new HttpClient()
            
            let client = new CloudflareClient(httpClient, ApiKey = options.ApiKey)
            let worker = new Worker(client, options)

            use _ = worker.AddressNotFoundEvent.Subscribe(fun args -> printfn "Network interface '%s' address not found." args.InterfaceName)
            use _ = worker.MulitAddressesEvent.Subscribe(fun args -> printfn "Mulit addresses '%s'." (String.Join(", ", args.Addresses)))
            use _ = worker.SameAddressEvent.Subscribe(fun _ -> printfn "Same address.")
            use _ = worker.NewAddressUploadedEvent.Subscribe(fun args -> printfn "Upload new address '%s' succeed." args.Address)
            use _ = worker.UnhandledExceptionEventArgs.Subscribe(fun args ->
                 printfn "Exception occurred." 
                 printfn "%O" args.Exception)
            use _ = worker.InterfaceNotFoundEvent.Subscribe(fun args ->
                printfn "Network interface '%s' not found." args.InterfaceName
                printfn "All network interfaces:"
                args.Interfaces |> Seq.iter (fun i ->
                    printfn "%s: %s" i.Name (String.Join(", ", Worker.GetInterfaceAddresses i)))
                )

            use _ = Console.CancelKeyPress.Subscribe(fun _ -> cts.Cancel())
            worker.ExecuteAsync(cts.Token).Wait()

        exitCode
#else
    let createHostBuilder args =
        Host.CreateDefaultBuilder(args)
            .ConfigureHostConfiguration(fun builder -> builder.AddUserSecrets(Assembly.GetExecutingAssembly()) |> ignore)
            .UseSystemd()
            .UseWindowsService(fun options -> options.ServiceName <- "Cloudflare DDNS")
            .ConfigureServices(fun hostContext services ->
                services.Configure<WorkerOptions>(hostContext.Configuration) |> ignore
                services.AddHttpClient() |> ignore
                services.AddHttpClient(
                    fun client (serviceProvider: IServiceProvider) ->
                        let apiKey = serviceProvider.GetRequiredService<IOptions<WorkerOptions>>().Value.ApiKey
                        new CloudflareClient(client, ApiKey = apiKey)) |> ignore
                services.AddHostedService<BackgroundWorker>() |> ignore)

    [<EntryPoint>]
    let main args =
        let host = createHostBuilder(args).Build()

        let command (host: IHost) =
            let options = host.Services.GetRequiredService<IOptions<WorkerOptions>>()
            let options = options.Value
            command options

        if command host |> not then host.Run()

        exitCode
#endif
