namespace CloudflareDDNS;

public class WorkerOptions
{
    public string? InterfaceName { get; set; }
    public string? Domain { get; set; }
    public string? RecordId { get; set; }
    public TimeSpan Period { get; set; }
}
