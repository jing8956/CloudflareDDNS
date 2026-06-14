using Meziantou.Framework.Win32;
using System.Runtime.Versioning;

namespace CloudflareDDNS;

[SupportedOSPlatform("windows5.1.2600")]
internal class CredentialConfigurationProvider : ConfigurationProvider
{
    public override void Load()
    {
        var credential = CredentialManager.ReadCredential("CloudflareDDNS", CredentialType.Generic);

        if (credential is not null)
        {
            Data["CloudflareDDNS:ZoneId"] = credential.UserName;
            Data["CloudflareDDNS:ApiKey"] = credential.Password;
        }
    }
}
