using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CloudflareDDNS;

public class Worker : BackgroundService
{
    private readonly CloudflareClient _client;
    private readonly WorkerOptions _options;
    private readonly IHost _host;
    private readonly ILogger<Worker> _logger;

    public Worker(
        CloudflareClient cloudflareClient,
        IOptions<WorkerOptions> options,
        IHost host, ILogger<Worker> logger)
    {
        _client = cloudflareClient;
        _options = options.Value;
        _host = host;
        _logger = logger;
    }

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
        var interfaceName = _options.InterfaceName;
        var recordId = _options.RecordId;

        if (interfaceName is null)
        {
            Log.InterfaceNameIsNull(_logger);

            for (; ; )
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                // 查找第一个具有公网 IPv6 地址的网络接口
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
                    Log.NetworkInterfaceNotFound(_logger);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                break;
            }
        }

        if (recordId == null)
        {
            Log.RecordIdIsNull(_logger);

            for (; ; )
            {
                var domain = _options.Domain;
                if (domain == null)
                {
                    domain = Dns.GetHostEntry("localhost").HostName;
                    Log.DomainIsNull(_logger, domain);
                }

                try
                {
                    var records = await _client.FindRecordsAsync(domain);
                    switch (records.Length)
                    {
                        case 0:
                            Log.NoRecordFound(_logger);
                            await _host.StopAsync(stoppingToken);
                            return;
                        case 1:
                            recordId = records[0].Id;
                            Log.FoundRecordId(_logger, recordId);
                            break;
                        default:
                            foreach (var item in records)
                            {
                                Log.MulitiRecordsFound(_logger, item.Id, item.Content);
                            }
                            await _host.StopAsync(stoppingToken);
                            return;
                    }

                    break;
                }
                catch (Exception e)
                {
                    Log.GetZoneIdFailed(e, _logger);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        using var timer = new PeriodicTimer(_options.Period);
        var recordIp = "";
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!GetNetworkInterface(interfaceName, out var nic))
                {
                    Log.NetworkInterfaceNotFound(_logger, interfaceName);
                    continue;
                }

                if (!GetAddress(nic, out var address))
                {
                    Log.AddressNotFound(_logger);
                    continue;
                }

                if (recordIp == address)
                {
                    Log.SameIPAddress(_logger);
                    continue;
                }

                await _client.SetAddress(recordId, address);
                recordIp = address;
                Log.UpdateNewIp(_logger, address);
            }
            catch (Exception e)
            {
                Log.UpdateNewIpFailed(e, _logger);
            }
            finally
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
    }
}
