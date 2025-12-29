using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace CloudflareDDNS;

public class Worker(
    CloudflareClient cloudflareClient,
    IOptions<WorkerOptions> options,
    IHost host, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interfaceName = options.Value.InterfaceName;
        var recordId = options.Value.RecordId;

        if (interfaceName == null)
        {
            Log.InterfaceNameIsNull(logger);

            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            interfaceName = interfaces.Where(i =>
            {
                return i.GetIPProperties().UnicastAddresses
                .Where(info => info.PrefixLength == 64)
                .Select(info => info.Address)
                .Where(addr => addr.AddressFamily == AddressFamily.InterNetworkV6)
                .Any(addr => !addr.IsIPv6LinkLocal);
            }).Select(i => i.Name).FirstOrDefault();

            if (interfaceName == null)
            {
                Log.NetworkInterfaceNotFound(logger);
                await host.StopAsync(stoppingToken);
                return;
            }

            Log.FoundInterface(logger, interfaceName);
        }
        if(recordId == null)
        {
            Log.RecordIdIsNull(logger);

            var domain = options.Value.Domain;
            if (domain == null)
            {
                domain = Dns.GetHostEntry("localhost").HostName;
                Log.DomainIsNull(logger, domain);
            }

            var records = await cloudflareClient.FindRecordsAsync(domain);
            switch(records.Length)
            {
                case 0:
                    Log.NoRecordFound(logger);
                    await host.StopAsync(stoppingToken);
                    return;
                case 1:
                    recordId = records[0].Id;
                    Log.FoundRecordId(logger, recordId);
                    break;
                default:
                    foreach (var item in records)
                    {
                        Log.MulitiRecordsFound(logger, item.Id, item.Content);
                    }
                    await host.StopAsync(stoppingToken);
                    return;
            }
        }

        using var timer = new PeriodicTimer(options.Value.Period);
        var recordIp = "";
        while (!stoppingToken.IsCancellationRequested)
        {
            var allInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            var nic = allInterfaces.FirstOrDefault(i => i.Name == interfaceName);
            if(nic == null)
            {
                Log.NetworkInterfaceNotFound(logger, interfaceName);
                continue;
            }

            var addr = nic.GetIPProperties().UnicastAddresses
                .Where(i => !OperatingSystem.IsWindows() || i.DuplicateAddressDetectionState == DuplicateAddressDetectionState.Preferred)
                .Where(i => i.Address.AddressFamily == AddressFamily.InterNetworkV6)
                .Where(i => !i.Address.IsIPv6LinkLocal)
                .Where(i => i.PrefixLength == 64)
                .Where(i => !OperatingSystem.IsWindows() || i.PrefixOrigin == PrefixOrigin.RouterAdvertisement)
                .OrderByDescending(i => OperatingSystem.IsWindows() ? i.AddressPreferredLifetime : 0L)
                .Select(i => i.Address.ToString())
                .FirstOrDefault();
            
            if(addr == null)
            {
                Log.AddressNotFound(logger);
                continue;
            }

            if(recordIp != addr)
            {
                try
                {
                    await cloudflareClient.SetAddress(recordId, addr);
                    recordIp = addr;
                    Log.UpdateNewIp(logger, addr);
                }
                catch (Exception e)
                {
                    Log.UpdateNewIpFailed(e, logger);
                }
            }
            else
            {
                Log.SameIPAddress(logger);
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
