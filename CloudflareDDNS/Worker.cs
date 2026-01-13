using System.Diagnostics.CodeAnalysis;
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
    private static bool GetNetworkInterface(string interfaceName, [NotNullWhen(true)] out NetworkInterface? nic)
    {
        var allInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var item in allInterfaces)
        {
            if (item.Name == interfaceName)
            {
                nic = item;
                return true;
            }
        }

        nic = null;
        return false;
    }

    private static bool GetAddress(NetworkInterface nic, [NotNullWhen(true)] out string? address)
    {
        address = nic.GetIPProperties().UnicastAddresses
            .Where(static i => !OperatingSystem.IsWindows() || i.DuplicateAddressDetectionState == DuplicateAddressDetectionState.Preferred)
            .Where(static i => i.Address.AddressFamily == AddressFamily.InterNetworkV6)
            .Where(static i => !i.Address.IsIPv6LinkLocal)
            .Where(static i => i.PrefixLength == 64)
            .Where(static i => !OperatingSystem.IsWindows() || i.PrefixOrigin == PrefixOrigin.RouterAdvertisement)
            .OrderByDescending(static i => OperatingSystem.IsWindows() ? i.AddressPreferredLifetime : 0L)
            .Select(static i => i.Address.ToString())
            .FirstOrDefault();
        return address is not null;
    }

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
        if (recordId == null)
        {
            Log.RecordIdIsNull(logger);

            var domain = options.Value.Domain;
            if (domain == null)
            {
                domain = Dns.GetHostEntry("localhost").HostName;
                Log.DomainIsNull(logger, domain);
            }

            var records = await cloudflareClient.FindRecordsAsync(domain);
            switch (records.Length)
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
            try
            {
                if (!GetNetworkInterface(interfaceName, out var nic))
                {
                    Log.NetworkInterfaceNotFound(logger, interfaceName);
                    continue;
                }

                if (!GetAddress(nic, out var address))
                {
                    Log.AddressNotFound(logger);
                    continue;
                }

                if (recordIp == address)
                {
                    Log.SameIPAddress(logger);
                    continue;
                }

                await cloudflareClient.SetAddress(recordId, address);
                recordIp = address;
                Log.UpdateNewIp(logger, address);
            }
            catch (Exception e)
            {
                Log.UpdateNewIpFailed(e, logger);
            }
            finally
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
    }
}
