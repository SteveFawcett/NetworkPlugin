using BroadcastPluginSDK;
using BroadcastPluginSDK.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Networks.Classes;
using System.Diagnostics.Eventing.Reader;
using System.Windows.Forms;
using static Networks.Classes.NetworkScanner;

namespace Networks.Panels
{
    public partial class NetworkPanel : UserControl, IInfoPage
    {
        private string Subnet = "192.168.1";
        private event EventHandler<List<NetworkDevice>>? NetworkDataUpdated;
        private List<NetworkDevice> _activeIPs = [];

        private readonly ILogger<IPlugin> _logger;
        private readonly NetworkScanner _scanner;
        private readonly IConfiguration _configuration;

        public NetworkPanel(IConfiguration configuration , ILogger<IPlugin> logger, NetworkScanner scanner)
        {
            _logger = logger;
            _scanner = scanner;
            _configuration = configuration;

            var Refresh = _configuration.GetValue<int?>("NetworkRefresh") ?? 60000;
            var Update = _configuration.GetValue<int?>("NetworkUpdate") ?? 1000;
            Subnet = _configuration.GetValue<string?>("NetworkSubnet") ?? "192.168.1";

            InitializeComponent();

            SetupListView();

            NetworkDataUpdated += NetworkData;

            NetworkRefreshTimer = new System.Threading.Timer(
                callback => LoadAsym() ,
                null,
                dueTime: 0,         // 🔥 Fire immediately
                period: Refresh
            );

            NetworkUpdateTimer = new System.Threading.Timer(
                async _ => await UpdateAsync(),
                null,
                dueTime: 0,         // 🔥 Fire immediately
                period: Update
            );
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

        public void  LoadAsym(CancellationToken cancellationToken = default)
        {
            _activeIPs = _scanner.ScanNetwork( Subnet);
            _activeIPs = _activeIPs.OrderByDescending(d => d.MemoryUsedPercent).ToList();
      
            _logger.LogInformation("Network loaded with {Count} entries", _activeIPs.Count);

            NetworkDataUpdated?.Invoke(this, _activeIPs);

        }
        public async Task UpdateAsync( CancellationToken cancellationToken = default)
        {
            var _logs = await _scanner.ScanNetworkAsync(  _activeIPs );

            _logger.LogInformation("Network loaded with {Count} entries", _logs.Count);

            NetworkDataUpdated?.Invoke(this, _logs);
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

        }
        private void NetworkData(object? e, List<NetworkDevice> logs)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => NetworkData(e, logs)));
                return;
            }

            ListNetwork.BeginUpdate();
            ListNetwork.SuspendLayout();

            foreach (var log in logs)
            {
                var existingItem = ListNetwork.Items.Cast<ListViewItem>().FirstOrDefault(i => i.SubItems[0].Text == log.IPAddress.ToString() );
                if (existingItem != null && !string.IsNullOrEmpty(existingItem.Text))
                {
                    if ( string.IsNullOrEmpty(log.Uptime))
                    {
                        int i = existingItem.Index;
                        ListNetwork.Items.RemoveAt(i);
                        continue;
                    }
                    if (!string.IsNullOrEmpty(log.HostName)) existingItem.SubItems[1].Text = log.HostName;
                    if (!string.IsNullOrEmpty(log.Uptime)) existingItem.SubItems[2].Text = log.Uptime;
                    existingItem.SubItems[3].Text = log.MemoryUsedPercent.ToString();
                    existingItem.BackColor = log.MemoryUsedPercent < 80 ? Color.LightGreen : Color.MistyRose;
                }
                else
                {
                    if (string.IsNullOrEmpty(log.Uptime)) continue;

                    var item = new ListViewItem(log.IPAddress.ToString());
                    item.SubItems.Add(log.HostName ?? string.Empty);
                    item.SubItems.Add(log.Uptime ?? string.Empty);
                    item.SubItems.Add(log.MemoryUsedPercent.ToString());

                    item.BackColor = log.MemoryUsedPercent < 80 ? Color.LightGreen : Color.MistyRose;

                    log.Index = ListNetwork.Items.Count - 1;
                    ListNetwork.Items.Add(item);
                }
            }

            ListNetwork.ResumeLayout();
            ListNetwork.EndUpdate();
        }
    }
}
