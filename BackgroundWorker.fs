namespace CloudflareDDNS

open System
open System.Net.NetworkInformation
open System.Text
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options

type BackgroundWorker(client: CloudflareClient, options: IOptions<WorkerOptions>, logger: ILogger<BackgroundWorker>) =
    inherit BackgroundService()

    override _.ExecuteAsync(stoppingToken) =
        task{
            let worker = new Worker(client, options.Value)
            use _ = worker.AddressNotFoundEvent.Subscribe(fun args -> logger.LogWarning("Network interface '{InterfaceName}' address not found.", args.InterfaceName))
            use _ = worker.MulitAddressesEvent.Subscribe(fun args -> logger.LogWarning("Mulit addresses '{Addresses}'.", String.Join(", ", args.Addresses)))
            use _ = worker.SameAddressEvent.Subscribe(fun _ -> logger.LogInformation("Same address."))
            use _ = worker.NewAddressUploadedEvent.Subscribe(fun args -> logger.LogInformation("Upload new address '{Address}' succeed.", args.Address))
            use _ = worker.UnhandledExceptionEventArgs.Subscribe(fun args -> logger.LogError(args.Exception, "Exception occurred."))
            use _ = worker.InterfaceNotFoundEvent.Subscribe(fun args ->
                logger.LogWarning("Network interface '{InterfaceName}' not found.", args.InterfaceName)
                (StringBuilder(), args.Interfaces)
                ||> Seq.fold (fun sb i -> sb.AppendFormat("{0}:{1}\r\n", i.Name, String.Join(", ", Worker.GetInterfaceAddresses i)))
                |> fun sb -> logger.LogInformation("All network interfaces:\r\n\r\n{AllNetworkInterfaces}", sb.ToString()))

            do! worker.ExecuteAsync(stoppingToken)
        }
