$params = @{
  Name = "ddns"
  BinaryPathName = "$(Get-Location)\CloudflareDDNS.exe"
  DependsOn = @("Dnscache", "TcpIP")
  DisplayName = "Cloudflare DDNS"
  StartupType = "AutomaticDelayedStart"
  Description = "Cloudflare DDNS Service"
}

New-Service @params