using CloudflareDDNS;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.Add<CredentialConfigurationSource>(static _ => { });
builder.Services.AddWindowsService(static options => options.ServiceName = "Cloudflare DDNS");

builder.Services.Configure<CloudflareClientOptions>(builder.Configuration.GetSection("CloudflareDDNS"));
builder.Services.AddTransient<IValidateOptions<CloudflareClientOptions>, CloudflareClientValidateOptions>();

builder.Services.AddHttpClient<CloudflareClient>(static (provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<CloudflareClientOptions>>().Value;
    var baseUrl = $"https://api.cloudflare.com/client/v4/zones/{options.ZoneId}/";

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Authorization = new("Bearer", options.ApiKey);
});

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("CloudflareDDNS"));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
