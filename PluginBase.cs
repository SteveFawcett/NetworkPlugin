using BroadcastPluginSDK.abstracts;
using BroadcastPluginSDK.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Networks.Panels;
using Networks.Classes;
using NetworkPlugin.Properties;

namespace Networks
{
    public class PluginBase : BroadcastPluginBase 
    {
        private const string STANZA = "Network";
        private ILogger<IPlugin>? _logger;
        private IConfiguration? _configuration;
        private static NetworkPanel? _updateForm;
        public PluginBase() : base() { } //  0 Parameter plugin for registration purposes.

        public PluginBase(IConfiguration configuration, ILogger<IPlugin> logger) :
            base(configuration, ListPage( configuration, logger ), Resources.green, STANZA)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public static NetworkPanel ListPage(IConfiguration configuration, ILogger<IPlugin> logger)
        {
            var scanner = new NetworkScanner(configuration);
         
            _updateForm = new NetworkPanel(configuration , logger, scanner);

            return _updateForm;
        }

        public event EventHandler<UserControl>? ShowScreen;
        private void OnOpenClicked(object? sender, EventArgs e)
        {
            if (_updateForm != null)
                ShowScreen?.Invoke(this, _updateForm);
        }
    }
}