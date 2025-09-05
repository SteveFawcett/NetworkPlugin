using BroadcastPluginSDK.Interfaces;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;


namespace Networks.Classes
{
    public class IPv4NetworkScanner
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public IPv4NetworkScanner(IConfiguration configuration, ILogger<IPlugin> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }
        public async Task<HashSet<NetworkDevice>> DiscoverDevicesAsync()
        {
            return await DiscoverIPv4DevicesOnInterfaceAsync();
        }

        async Task<HashSet<NetworkDevice>> DiscoverIPv4DevicesOnInterfaceAsync()
        {
            var subnet = _configuration.GetValue<string>("Network:Subnet") ?? "192.168.1";
            var discoveredIPs = new HashSet<NetworkDevice>();
            var tasks = new List<Task>();

            for (int i = 1; i <= 254; i++)
            {
                string ip = $"{subnet}.{i}";

                using var ping = new Ping();
                try
                {
                    var reply = await ping.SendPingAsync(ip, 1000);
                    if (reply.Status == IPStatus.Success && reply.Address != null)
                    {
                        var device = new NetworkDevice { IPAddress = reply.Address , Interface = "IPV6"};
                        discoveredIPs.Add( device );
                    }
                }
                catch (SocketException ex)
                {
                    _logger.LogError($"Socket error while pinging: {ex.Message}");
                }

            }

            return discoveredIPs;
        }
    }
}
