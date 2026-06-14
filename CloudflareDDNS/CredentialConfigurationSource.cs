using System.Runtime.Versioning;

namespace CloudflareDDNS;

[SupportedOSPlatform("windows5.1.2600")]
internal class CredentialConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new CredentialConfigurationProvider();
    }
}
