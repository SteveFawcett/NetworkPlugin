using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;


namespace Networks.Classes
{
    public class SnmpDeviceInfo
    {
        public string Description { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Uptime { get; set; } = string.Empty;
    }

    public class NetworkScanner
    {
        // This class represents a detected device
        public class NetworkDevice
        {
            public int Index { get; set; } = 0;
            public IPAddress IPAddress { get; set; } = IPAddress.None;
            public string? HostName { get; set; }
            public string? Description { get; set; }
            public string? Name { get; set; }
            public string? Uptime { get; set; }
            public double MemoryUsedPercent { get; set; }
        }

        private readonly IConfiguration _configuration;
        public NetworkScanner(IConfiguration configuration)
        {
            _configuration = configuration;
        }
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
            catch
            {
                return -1;
            }
        }

        public NetworkDevice? QuerySnmp(IPAddress ipAddress, string community = "public")
        {
            var endpoint = new IPEndPoint( ipAddress, 161);

            var nc = WalkMemoryTable(endpoint);
  
            var oids = new List<Variable>
            {
                new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.1.0")), // sysDescr
                new Variable(new ObjectIdentifier("1.3.6.1.2.1.1.5.0")),  // sysName
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
                using var cts = new CancellationTokenSource(1000);
                var result =  Messenger.GetAsync(VersionCode.V2, endpoint, new OctetString(community), oids , cts.Token).Result;

                NetworkDevice networkDevice = new NetworkDevice
                {
                    Description = result[0].Data.ToString(),
                    Name = result[1].Data.ToString(),
                    Uptime = result[2].Data.ToString(),
                };

                if (nc != -1 && result.Count >= 6)
                {
                    // Use long to avoid overflow
                    long allocationUnitSize = Convert.ToInt64(result[3].Data.ToString());
                    long totalUnits = Convert.ToInt64(result[4].Data.ToString());
                    long usedUnits = Convert.ToInt64(result[5].Data.ToString());

                    long totalMemoryBytes = allocationUnitSize * totalUnits;
                    long usedMemoryBytes = allocationUnitSize * usedUnits;

                    networkDevice.MemoryUsedPercent = Math.Round( (double)usedMemoryBytes / totalMemoryBytes * 100);

                }
                return networkDevice;   

            }
            catch (Exception)
            {
                return new NetworkDevice { };
            }
 
        }


    // Method to scan subnet and return active devices
        public List<NetworkDevice> ScanNetwork(string subnet, int start = 1, int end = 254)
        {
            var devices = new List<NetworkDevice>();
            var tasks = new List<Task>();
            for (int i = start; i <= end; i++)
            {
                string ip = $"{subnet}.{i}";
   
                tasks.Add(Task.Run(async () =>
                {
                    if (await PingAsync(ip))
                    {
                        NetworkDevice SnmpInfo = new();

                        string hostName = await TryGetHostNameAsync(ip);
                        lock (devices)
                        {
                            SnmpInfo.IPAddress = IPAddress.Parse(ip);
                            SnmpInfo.HostName = hostName;
                            devices.Add(SnmpInfo);
                        }
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
            return devices;
        }
        public async Task<List<NetworkDevice>> ScanNetworkAsync(List<NetworkDevice> devices)
        {
            var tasks = new List<Task>();

            foreach (var device in devices)
            {
                if (device.IPAddress == null) continue;

                tasks.Add(Task.Run(() =>
                {
                    var SnmpInfo = QuerySnmp(device.IPAddress);
                    if (SnmpInfo == null) return;

                    device.Description = SnmpInfo.Description;
                    device.Name = SnmpInfo.Name;
                    device.Uptime = SnmpInfo.Uptime;
                    device.MemoryUsedPercent = SnmpInfo.MemoryUsedPercent;
                }));
            }

            await Task.WhenAll(tasks);
            return devices;
        }

        private async Task<bool> PingAsync(string ip)
        {
            using (var ping = new Ping())
            {
                try
                {
                    var reply = await ping.SendPingAsync(ip, 1000);
                    return reply.Status == IPStatus.Success;
                }
                catch
                {
                    return false;
                }
            }
        }

        private async Task<string> TryGetHostNameAsync(string ip)
        {
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip);
                return entry.HostName;
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
