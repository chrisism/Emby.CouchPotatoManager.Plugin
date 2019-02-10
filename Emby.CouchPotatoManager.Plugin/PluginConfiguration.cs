using MediaBrowser.Model.Plugins;

namespace Emby.CouchPotatoManager.Plugin
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string ServerUrl { get; set; }

        public string ApiKey { get; set; }

        public int DelayTimeInSeconds { get; set; }
    }
}
