using BroadcastPluginSDK.Interfaces;
using Lextm.SharpSnmpLib;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Networks.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Networks.Classes
{
    public class NetworkDevice
    {
        public required IPAddress IPAddress;
        public required string Interface;
        public string? Description;
        public string? Name;
        public string? Uptime;
        public double? MemoryUsedPercent;
    };
    internal class BoundList
    {
        private SNMP _snmp;
        private IConfiguration _config;
        private ILogger<IPlugin> _logger;

        public class NetworkDeviceEventArgs : EventArgs
        {
            public NetworkDevice Device { get; }
            public NetworkDeviceEventArgs(NetworkDevice device)
            {
                Device = device;
            }
        }

        public event EventHandler<NetworkDeviceEventArgs>? DeviceAdded;
        public event EventHandler<NetworkDeviceEventArgs>? DeviceChanged;
        public event EventHandler<NetworkDeviceEventArgs>? DeviceDeleted;

        static BindingList<NetworkDevice> items = new BindingList<NetworkDevice>();

        public BoundList(IConfiguration c , ILogger<IPlugin> p )
        {
            _snmp = new SNMP(c, p); 
            _config = c;
            _logger = p;

            items.ListChanged += (s, e) =>
            {
                if (e.ListChangedType == ListChangedType.ItemAdded)
                {
                    var device = items[e.NewIndex];
                    _logger.LogInformation("ADDED hostname for {IP}: {HostName}", device.IPAddress , device.Name);
                    DeviceAdded?.Invoke(this, new NetworkDeviceEventArgs(device));
                }

                if (e.ListChangedType == ListChangedType.ItemChanged)
                {
                    var device = items[e.NewIndex];
                    _logger.LogDebug("CHANGED hostname for {IP}: {HostName}", device.IPAddress, device.Name);
                    DeviceChanged?.Invoke(this, new NetworkDeviceEventArgs(device));
                }

                if (e.ListChangedType == ListChangedType.ItemDeleted)
                {
                    var device = items[e.NewIndex];
                    _logger.LogDebug("DELETED hostname for {IP}: {HostName}", device.IPAddress, device.Name);
                    DeviceDeleted?.Invoke(this, new NetworkDeviceEventArgs(device));
                }
            };
        }

        public NetworkDevice? FindByIp(IPAddress ip)
        {
            foreach (var d in items)
            {
                if (d.IPAddress.Equals(ip))
                    return d;
            }
            return null;
        }
        public BindingList<NetworkDevice> Items => items;
        public void Add(HashSet<NetworkDevice> devices)
        {
            foreach (var device in devices)
            {
                Add(device);
            }
        }

        [DebuggerNonUserCode]
        private void Add(NetworkDevice newDevice)
        {
            NetworkDevice? existing = FindByIp(newDevice.IPAddress);

            if (existing != null)
            {
                var enriched = _snmp.EnrichSnmpDeviceAsync(existing).GetAwaiter().GetResult();

                _logger.LogDebug("Enriching device {IP} with SNMP data", existing.IPAddress);
                
                if (existing.Description != enriched.Description ) existing.Description = enriched.Description;
                if (string.IsNullOrEmpty( existing.Name ) || existing.Name == "Unknown") existing.Name = _snmp.TryGetHostNameAsync(existing.IPAddress).GetAwaiter().GetResult();
                if (existing.Uptime != enriched.Uptime ) existing.Uptime = enriched.Uptime;
                if (existing.MemoryUsedPercent != enriched.MemoryUsedPercent ) existing.MemoryUsedPercent = enriched.MemoryUsedPercent;

                DeviceChanged?.Invoke(this, new NetworkDeviceEventArgs(existing));
                return;
            }

            var enrichedNew = _snmp.EnrichSnmpDeviceAsync(newDevice).GetAwaiter().GetResult();
            newDevice.Description = enrichedNew.Description;
            newDevice.Name = _snmp.TryGetHostNameAsync(newDevice.IPAddress).GetAwaiter().GetResult();
            newDevice.Uptime = enrichedNew.Uptime;
            newDevice.MemoryUsedPercent = enrichedNew.MemoryUsedPercent;

            items.Add(newDevice); // This triggers DeviceAdded via ListChanged
        }
    }
}
