using Meziantou.Framework.Win32;
using WixToolset.Dtf.WindowsInstaller;

namespace CloudflareDDNS.WixToolset.Utils
{
    partial class CustomActions
    {
        [CustomAction]
        public static ActionResult WriteCredential(Session session)
        {
            session.Log("Begin CloudflareDDNS.WriteCredential");

            var zoneId = session.CustomActionData["ZONE_ID"];
            var apiKey = session.CustomActionData["API_KEY"];

            session.Log($"The Zone ID is '{zoneId}'");

            CredentialManager.WriteCredential(
                applicationName: ApplicationName,
                userName: zoneId,
                secret: apiKey,
                comment: "Cloudflare DDNS API Key",
                persistence: CredentialPersistence.LocalMachine,
                type: CredentialType.Generic);

            return ActionResult.Success;
        }
    }
}
