# CloudflareDDNS
Periodically obtain the current IPv6 address from the NIC and update Cloudflare DNS record if changed.

Parameters in appsettings.json:
- InterfaceName: Network interfaces name. You can find all network interface name in log when network interface not found.
- RecordId: Upload DNS reqcord id. Create reocrd first and use `CloudflareDDNS --domain my-machine.example.com --zoneid <zoneid> --apikey <apikey>` search it.
- ApiKey: Visit https://dash.cloudflare.com/profile/api-tokens to create a api key.
- ZoneId: Visit Cloudflare domain Overview page and find Zone ID.
- Period: Period for check. eg: 00:01:00

## Create Windows Service
```bat
sc.exe create "CloudflareDDNS" binpath="C:\Path\To\CloudflareDDNS.exe" DisplayName="Cloudflare DDNS" start=auto
```  


## Create Linux Service (Systemld) 
```ini
[Unit]
Description=Cloudflare DDNS
After=network.target

[Service]
Type=simple
Restart=always
ExecStart=/Path/To/CloudflareDDNS
WorkingDirectory=/Path/To

[Install]
WantedBy=multi-user.target
```
