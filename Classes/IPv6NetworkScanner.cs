using BroadcastPluginSDK.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;


namespace Networks.Classes;

public class IPv6NetworkScanner
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IPlugin> _logger;

    public IPv6NetworkScanner(IConfiguration configuration  , ILogger<IPlugin> logger )
    {
        _configuration = configuration;
        _logger = logger;
    }
    public HashSet<NetworkDevice> DiscoverDevicesAsync()
    {
        HashSet<NetworkDevice> discoveredIPs = new();

        _logger.LogDebug("Starting IPv6 network discovery...");

         foreach ( var kvp in GetIPv6NeighborsWithAdapters())
        {
            NetworkDevice device = new() { IPAddress = kvp.Key , Interface = kvp.Value.Name };
            discoveredIPs.Add(device);
        }

        return discoveredIPs;
    }

    [DebuggerNonUserCode]
    public Dictionary<IPAddress, NetworkInterface> GetIPv6NeighborsWithAdapters()
    {
        var result = new Dictionary<IPAddress, NetworkInterface>();

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "interface ipv6 show neighbors",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        int currentInterfaceIndex = -1;

        foreach (var line in output.Split('\n'))
        {
            var interfaceMatch = Regex.Match(line, @"Interface\s+(\d+):");
            if (interfaceMatch.Success)
            {
                currentInterfaceIndex = int.Parse(interfaceMatch.Groups[1].Value);
                continue;
            }

            var ipMatch = Regex.Match(line, @"([a-fA-F0-9:]+)");
            if (ipMatch.Success && IPAddress.TryParse(ipMatch.Value, out var ip))
            {
                try
                {
                    var adapter = NetworkInterface.GetAllNetworkInterfaces()
                        .FirstOrDefault(ni =>
                            ni.GetIPProperties().GetIPv6Properties()?.Index == currentInterfaceIndex);

                    if (adapter != null)
                    {
                        result[ip] = adapter;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing IP {IP}", ip);
                }
            }
        }

        return result;
    }
}

