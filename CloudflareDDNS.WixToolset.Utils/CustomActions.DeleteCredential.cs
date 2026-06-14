using Meziantou.Framework.Win32;
using WixToolset.Dtf.WindowsInstaller;

namespace CloudflareDDNS.WixToolset.Utils
{
    partial class CustomActions
    {
        [CustomAction]
        public static ActionResult DeleteCredential(Session session)
        {
            session.Log("Begin CloudflareDDNS.DeleteCredential");

            var credential = CredentialManager.ReadCredential(
                applicationName: ApplicationName,
                type: CredentialType.Generic);

            if (credential != null)
            {
                CredentialManager.DeleteCredential(
                    applicationName: ApplicationName,
                    type: CredentialType.Generic);
            }

            return ActionResult.Success;
        }
    }
}
