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

        if(interfaceName == null)
        {
            logger.LogWarning("InterfaceName is null, try find network interface.");

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
                logger.LogError("Network interface not found.");
                await host.StopAsync(stoppingToken);
                return;
            }

            logger.LogInformation("Found interface '{InterfaceName}'.", interfaceName);
        }
        if(recordId == null)
        {
            logger.LogWarning("RecordId is null, try find recrod id.");

            var domain = options.Value.Domain;
            if (domain == null)
            {
                domain = Dns.GetHostEntry("localhost").HostName;
                logger.LogWarning("Domain is null, try use hostname '{HostName}'.", domain);
            }

            var records = await cloudflareClient.FindRecordsAsync(domain);
            switch(records.Length)
            {
                case 0:
                    logger.LogError("No record found.");
                    await host.StopAsync(stoppingToken);
                    return;
                case 1:
                    recordId = records[0].Id;
                    logger.LogInformation("Found record id '{RecordId}'", recordId);
                    break;
                default:
                    foreach (var item in records)
                    {
                        logger.LogError("Muliti records found: {RecordId} {Content}", item.Id, item.Content);
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
                logger.LogWarning("NetworkInterface '{InterfaceName}' not found.", interfaceName);
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
                logger.LogWarning("Address not found.");
                continue;
            }

            if(recordIp != addr)
            {
                try
                {
                    await cloudflareClient.SetAddress(recordId, addr);
                    recordIp = addr;
                    logger.LogInformation("Update new ip '{IpAddress}' succeed.", addr);
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Update new ip address failed.");
                }
            }
            else
            {
                logger.LogDebug("Same addr");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
