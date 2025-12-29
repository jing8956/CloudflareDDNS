using System;
using System.Collections.Generic;
using System.Text;

namespace CloudflareDDNS;

internal static partial class Log
{
    [LoggerMessage(5000, LogLevel.Error, "Network interface not found.")]
    public static partial void NetworkInterfaceNotFound(ILogger logger);
    [LoggerMessage(5001, LogLevel.Error, "No record found.")]
    public static partial void NoRecordFound(ILogger logger);
    [LoggerMessage(5002, LogLevel.Error, "Muliti records found: {RecordId} {Content}.")]
    public static partial void MulitiRecordsFound(ILogger logger, string recordId, string content);

    [LoggerMessage(6000, LogLevel.Warning, "InterfaceName is null, try find network interface.")]
    public static partial void InterfaceNameIsNull(ILogger logger);
    [LoggerMessage(6001, LogLevel.Warning, "RecordId is null, try find recrod id.")]
    public static partial void RecordIdIsNull(ILogger logger);
    [LoggerMessage(6002, LogLevel.Warning, "Domain is null, try use hostname '{HostName}'.")]
    public static partial void DomainIsNull(ILogger logger, string hostName);
    [LoggerMessage(6003, LogLevel.Warning, "NetworkInterface '{InterfaceName}' not found.")]
    public static partial void NetworkInterfaceNotFound(ILogger logger, string interfaceName);
    [LoggerMessage(6004, LogLevel.Warning, "Address not found.")]
    public static partial void AddressNotFound(ILogger logger);
    [LoggerMessage(6005, LogLevel.Warning, "Address not found.")]
    public static partial void UpdateNewIpFailed(Exception exception, ILogger logger);

    [LoggerMessage(7000, LogLevel.Information, "Found interface '{InterfaceName}'.")]
    public static partial void FoundInterface(ILogger logger, string interfaceName);
    [LoggerMessage(7001, LogLevel.Information, "Found record id '{RecordId}'.")]
    public static partial void FoundRecordId(ILogger logger, string recordId);
    [LoggerMessage(7002, LogLevel.Information, "Update new ip '{IpAddress}' succeed.")]
    public static partial void UpdateNewIp(ILogger logger, string ipAddress);

    [LoggerMessage(8000, LogLevel.Debug, "Same IP address.")]
    public static partial void SameIPAddress(ILogger logger);
}
