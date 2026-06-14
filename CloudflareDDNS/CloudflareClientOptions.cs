using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace CloudflareDDNS;

internal class CloudflareClientOptions
{
    [Required(AllowEmptyStrings = false)]
    public string ZoneId { get; set; } = default!;

    [Required(AllowEmptyStrings = false)]
    public string ApiKey { get; set; } = default!;
}

[OptionsValidator]
internal partial class CloudflareClientValidateOptions : IValidateOptions<CloudflareClientOptions>
{

}
