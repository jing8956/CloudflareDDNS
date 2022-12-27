# CloudflareDDNS
```
CloudflareDDNS - periodically obtain the current IPv6 address from the NIC and update Cloudflare DNS record if changed.

usage: CloudflareDDNS -l | --list-interfaces
usage: CloudflareDDNS <interfaceName> <domain> <apiKey> <zoneId> [period]

Options:
  -l, --list-interfaces list all network interfaces
 
Parameters:
 <interfaceName> Required: network interfaces name, please run `CloudflareDDNS -l`.
 <domain>        Required: upload DNS reqcord domain. eg: my-machine.example.com
 <apiKey>        Required: visit https://dash.cloudflare.com/profile/api-tokens to create a api key.
 <zoneId>        Required: visit Cloudflare domain Overview page and find Zone ID.
 [period]        Optional: period for check. default: 00:01:00
```

## Create Linux Service (Systemld)
```
[Unit]
Description=Cloudflare DDNS
After=network.target

[Service]
Type=simple
Restart=always
ExecStart=CloudflareDDNS <interfaceName> <domain> <apiKey> <zoneId> [period]

[Install]
WantedBy=multi-user.target
```
