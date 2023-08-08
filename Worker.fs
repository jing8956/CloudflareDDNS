namespace CloudflareDDNS

open System
open System.Net.NetworkInformation
open System.Threading
open System.Threading.Tasks

type WorkerOptions() =
    member val InterfaceName = Unchecked.defaultof<string> with get, set
    member val Domain = Unchecked.defaultof<string> with get, set
    member val RecordId = Unchecked.defaultof<string> with get, set
    member val ApiKey = Unchecked.defaultof<string> with get, set
    member val ZoneId = Unchecked.defaultof<string> with get, set
    member val Period = Unchecked.defaultof<TimeSpan> with get, set

type AddressNotFoundEventArgs(interfaceName) = inherit EventArgs() member _.InterfaceName = interfaceName
type MulitAddressesEventArgs(addresses) = inherit EventArgs() member _.Addresses = addresses
type NewAddressUploadedEventArgs(address) = inherit EventArgs() member _.Address = address
type UnhandledExceptionEventArgs(ex) = inherit EventArgs() member _.Exception = ex
type InterfaceNotFoundEventArgs(interfaceName, interfaces) =
    inherit EventArgs()
    member _.InterfaceName = interfaceName
    member _.Interfaces = interfaces

type Worker(client: CloudflareClient, options: WorkerOptions) =
    static let isNotWindows = OperatingSystem.IsWindows() |> not

    let addressNotFoundEvent = new Event<AddressNotFoundEventArgs>()
    let mulitAddressesEvent = new Event<MulitAddressesEventArgs>()
    let sameAddressEvent = new Event<EventArgs>()
    let newAddressUploadedEvent = new Event<NewAddressUploadedEventArgs>()
    let unhandledExceptionEvent = new Event<UnhandledExceptionEventArgs>()
    let interfaceNotFoundEvent = new Event<InterfaceNotFoundEventArgs>()

    [<CLIEvent>] member _.AddressNotFoundEvent = addressNotFoundEvent.Publish
    [<CLIEvent>] member _.MulitAddressesEvent = mulitAddressesEvent.Publish
    [<CLIEvent>] member _.SameAddressEvent = sameAddressEvent.Publish
    [<CLIEvent>] member _.NewAddressUploadedEvent = newAddressUploadedEvent.Publish
    [<CLIEvent>] member _.UnhandledExceptionEventArgs = unhandledExceptionEvent.Publish
    [<CLIEvent>] member _.InterfaceNotFoundEvent = interfaceNotFoundEvent.Publish

    static member GetInterfaceAddresses(networkInterface: NetworkInterface) =
        networkInterface.GetIPProperties().UnicastAddresses
        |> Seq.where (fun i -> isNotWindows || i.DuplicateAddressDetectionState = DuplicateAddressDetectionState.Preferred)
        |> Seq.where (fun i -> i.Address.AddressFamily = System.Net.Sockets.AddressFamily.InterNetworkV6)
        |> Seq.where (fun i -> not i.Address.IsIPv6LinkLocal)
        |> Seq.where (fun i -> i.PrefixLength = 64)
        |> Seq.where (fun i -> isNotWindows || i.PrefixOrigin = PrefixOrigin.RouterAdvertisement)
        |> Seq.sortByDescending (fun i -> if isNotWindows then 0L else i.AddressPreferredLifetime)
        |> Seq.map (fun i -> i.Address.ToString())
    member _.ExecuteAsync(stoppingToken: CancellationToken) =
        task{
            let mutable recordIp = ""

            use timer = new PeriodicTimer(options.Period)
            while not stoppingToken.IsCancellationRequested do
                let allInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                match allInterfaces |> Seq.tryFind (fun i -> i.Name = options.InterfaceName) with
                | Some networkInterface ->
                    let addresses =  Worker.GetInterfaceAddresses networkInterface |> ResizeArray
                    if addresses.Count = 0 then
                        addressNotFoundEvent.Trigger(new AddressNotFoundEventArgs(options.InterfaceName))
                    else
                        if addresses.Count > 1 then mulitAddressesEvent.Trigger(new MulitAddressesEventArgs(addresses))
                        let address = addresses.[0]
                        if address = recordIp then sameAddressEvent.Trigger(EventArgs.Empty)
                        else
                            try
                                do! client.SetAddress(options.ZoneId, options.RecordId, address)
                                recordIp <- address
                                newAddressUploadedEvent.Trigger(new NewAddressUploadedEventArgs(address))
                            with
                            | e -> unhandledExceptionEvent.Trigger(new UnhandledExceptionEventArgs(e))
                | None -> interfaceNotFoundEvent.Trigger(new InterfaceNotFoundEventArgs(options.InterfaceName, allInterfaces))
                do! timer.WaitForNextTickAsync(stoppingToken).AsTask() :> Task
        } :> Task
