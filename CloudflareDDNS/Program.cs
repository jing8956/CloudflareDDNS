using CloudflareDDNS;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "Cloudflare DDNS");
builder.Services.AddHttpClient<CloudflareClient>(client =>
{
    var apiKey = builder.Configuration["ApiKey"];
    var zoneId = builder.Configuration["ZoneId"];
    var baseUrl = $"https://api.cloudflare.com/client/v4/zones/{zoneId}/";

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
});
builder.Services.Configure<WorkerOptions>(o =>
{
    var configuration = builder.Configuration;

    o.InterfaceName = configuration["InterfaceName"];
    o.Domain = configuration["Domain"];
    o.RecordId = configuration["RecordId"];
    o.Period = TimeSpan.Parse(configuration["Period"] ?? "00:01:00");
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
