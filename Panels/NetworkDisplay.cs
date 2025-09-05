using BroadcastPluginSDK.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Networks.Classes;
using System.Net;
using System.Net.NetworkInformation;

namespace Networks.Panels
{
    public partial class NetworkPanel : UserControl, IInfoPage
    {
        private event EventHandler<HashSet<IPAddress>>? NetworkDataUpdated;

        private readonly ILogger<IPlugin> _logger;
        private readonly IPv4NetworkScanner _ipv4Scanner;
        private readonly IPv6NetworkScanner _ipv6Scanner;
        private readonly IConfiguration _configuration;
        private BoundList _devices;

        public NetworkPanel(IConfiguration configuration , ILogger<IPlugin> logger, IPv4NetworkScanner scanner , IPv6NetworkScanner scanner2)
        {
            _logger = logger;
            _ipv4Scanner = scanner;
            _ipv6Scanner = scanner2;
            _configuration = configuration;
            _devices = new BoundList(configuration, logger);

            var Refresh = _configuration.GetValue<int?>("NetworkRefresh") ?? 1000;
            var Update = _configuration.GetValue<int?>("NetworkUpdate") ?? 10000;

            InitializeComponent();

            PopulateInterfaces();
            SetupListView();
            FilterListView(Interfaces.Text);

            // With the following corrected code:
            NetworkUpdateTimer = new System.Threading.Timer(
                callback => LoadIPv6Async(),
                null,
                dueTime: 0,         // 🔥 Fire immediately
                period: Update
            );

            Interfaces.SelectedIndexChanged += (s, e) =>
            {
                FilterListView(Interfaces.Text);
            };

            _devices.DeviceAdded += (s, e) =>
            {
                _logger.LogDebug("Device Added: {IP} - {Name}", e.Device.IPAddress, e.Device.Name ?? "Unknown");

                if (ListNetwork.InvokeRequired)
                {
                    ListNetwork.Invoke(new Action(() =>
                    {
                        FilterListView(Interfaces.Text);
                    }));
                }
                else
                {
                    FilterListView(Interfaces.Text);
                }
            };

        }

        public void FilterListView(string filterText)
        {
            if( filterText == "All Interfaces" )
            {
                filterText = string.Empty;
            }

            var filtered = _devices.Items
                .Where(d => d.Name?.Contains(filterText, StringComparison.OrdinalIgnoreCase) == true
                         || d.Interface.ToString().Contains(filterText))
                .ToList();

            ListNetwork.BeginUpdate();
            ListNetwork.Items.Clear();

            foreach (var device in filtered)
            {
                var item = new ListViewItem(device.IPAddress.ToString());
                item.SubItems.Add(device.Name ?? "Unknown");
                item.SubItems.Add(device.Uptime ?? "N/A");
                item.SubItems.Add(device.MemoryUsedPercent?.ToString("F1") + "%" ?? "—");
                item.SubItems.Add(device.Interface ?? "N/A");
                ListNetwork.Items.Add(item);
            }

            ListNetwork.EndUpdate();
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                NetworkRefreshTimer?.Dispose();
            }

            base.Dispose(disposing);
        }

        public Control GetControl()
        {
            return this;
        }
        public void LoadIPv6Async( CancellationToken cancellationToken = default)
        {
            var _activeIPs = _ipv6Scanner.DiscoverDevicesAsync();

            _logger.LogDebug("Network loaded with {Count} IPv6 entries", _activeIPs.Count);

            _devices.Add(_activeIPs);
        }

        private void SetupListView()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(SetupListView));
                return;
            }
            ListNetwork.Columns.Clear();
            ListNetwork.Columns.Add("IP Address", 100, HorizontalAlignment.Left);
            ListNetwork.Columns.Add("Name", 100, HorizontalAlignment.Left);
            ListNetwork.Columns.Add("Uptime", 100, HorizontalAlignment.Left);
            ListNetwork.Columns.Add("Mem Usage (%)", 100, HorizontalAlignment.Left);
            ListNetwork.Columns.Add("Interface", 100, HorizontalAlignment.Left);
        }

        private void PopulateInterfaces()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(PopulateInterfaces));
                return;
            }
            Interfaces.Items.Clear();
            Interfaces.Items.Add("All Interfaces");
            Interfaces.SelectedIndex = 0;

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                Interfaces.Items.Add(ni.Name);
            }
        }
    }
}
