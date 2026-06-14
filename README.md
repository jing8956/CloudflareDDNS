# CloudflareDDNS
Periodically obtain the current IPv6 address from the NIC and update Cloudflare DNS record if changed.

This program performs the following actions:
1. Obtains the current computer's FQDN upon startup
2. Checks if a unique DNS record with the corresponding ID exists on Cloudflare based on the FQDN
3. Attempts to obtain the first NIC with a public IPv6 address
4. Updates the address on Cloudflare
5. Afterwards, it checks the address every minute to see if it has changed
6. If the address has changed, it updates the address accordingly.

## Install
1. Add the computer to Active Directory or add DNS suffix.
2. Create a AAAA record in Cloudflare DNS. 
3. Get Cloudflare zone id and create api key.
4. Use the following PowerShell 7 script to install the program.
```pwsh
$zoneId = Read-Host -Prompt "Please input Cloudflare Zone ID"
$apiKey = Read-Host -Prompt "Please input Cloudflare API Key" -MaskInput
msiexec.exe /i CloudflareDDNS-4.0.0-win-x64.msi CLOUDFLARE_ZONE_ID=$zoneId CLOUDFLARE_API_KEY=$apiKey
```  