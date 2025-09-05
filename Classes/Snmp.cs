using BroadcastPluginSDK.Interfaces;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;


namespace Networks.Classes
{
    public class SNMP
    {
        // This class represents a detected devic

        private readonly IConfiguration _configuration;
        private readonly ILogger<IPlugin> _logger;
        public SNMP(IConfiguration configuration , ILogger<IPlugin> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [DebuggerNonUserCode]
        public int WalkMemoryTable(IPEndPoint endpoint, string community = "public", int timeoutMs = 1000)
        {
            var result = new List<Variable>();

            try
            {
                Messenger.Walk(
                    VersionCode.V2,
                    endpoint,
                    new OctetString(community),
                    new ObjectIdentifier("1.3.6.1.2.1.25.2.3.1"), // hrStorageTable
                    result,
                    timeoutMs,
                    WalkMode.WithinSubtree);

                foreach (var variable in result)
                {
                    if (variable.Data.ToString().Contains("Physical Memory"))
                    {
                        var oidParts = variable.Id.ToString().Split('.');
                        return int.Parse(oidParts.Last());
                    }
                }
                return -1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected SNMP error while walking {Endpoint}", endpoint);
                return -1;
            }
        }

        [DebuggerNonUserCode]
        public async Task<NetworkDevice> EnrichSnmpDeviceAsync(NetworkDevice nd, string community = "public")
        {
            var endpoint = new IPEndPoint(nd.IPAddress, 161);
            var nc = WalkMemoryTable(endpoint);

            var oids = new List<Variable>
            {
                new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.1.0")), // sysDescr
                new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.5.0")), // sysName
                new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.3.0")), // sysUpTime
            };

            if (nc != -1)
            {
                oids.Add(new Variable(new ObjectIdentifier($"1.3.6.1.2.1.25.2.3.1.4.{nc}"))); // AllocationUnit
                oids.Add(new Variable(new ObjectIdentifier($"1.3.6.1.2.1.25.2.3.1.5.{nc}"))); // TotalUnits
                oids.Add(new Variable(new ObjectIdentifier($"1.3.6.1.2.1.25.2.3.1.6.{nc}"))); // UsedUnits
            }

            try
            {
                using var cts = new CancellationTokenSource(2000);

                IList<Variable> result ;
                try
                {
                    result = await Messenger.GetAsync(
                        VersionCode.V2,
                        endpoint,
                        new OctetString(community),
                        oids,
                        cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogWarning("SNMP request timed out: {Message}", ex.Message);
                    return nd;
                }
                catch (SocketException ex)
                {
                    _logger.LogWarning("Socket error while querying {IP}: {Message}", nd.IPAddress, ex.Message);
                    return nd;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected SNMP error while querying {Endpoint}", endpoint);
                    return nd;
                }

                if (result.Count >= 3)
                {
                    nd.Description = result[0].Data.ToString();
                    nd.Name = result[1].Data.ToString();
                    nd.Uptime = result[2].Data.ToString();

                    _logger.LogInformation("SNMP for {IP} - {Name} - {Uptime}", nd.IPAddress, nd.Name, nd.Uptime);
                }

                if (nc != -1 && result.Count >= 6)
                {
                    long allocationUnitSize = Convert.ToInt64(result[3].Data.ToString());
                    long totalUnits = Convert.ToInt64(result[4].Data.ToString());
                    long usedUnits = Convert.ToInt64(result[5].Data.ToString());

                    long totalMemoryBytes = allocationUnitSize * totalUnits;
                    long usedMemoryBytes = allocationUnitSize * usedUnits;

                    nd.MemoryUsedPercent = Math.Round((double)usedMemoryBytes / totalMemoryBytes * 100, 2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("SNMP Timeout or error for {IP}: {Message}", nd.IPAddress, ex.Message);
            }

            return nd;
        }

        [DebuggerNonUserCode]
        public async Task<string> TryGetHostNameAsync(IPAddress ip)
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip);
                return entry.HostName;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
