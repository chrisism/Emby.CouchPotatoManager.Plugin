using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Emby.CouchPotatoManager.Plugin.Domain;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;

namespace Emby.CouchPotatoManager.Plugin.Tasks
{
    public class SearchWantedTask : IScheduledTask
    {
        private readonly IJsonSerializer serializer;

        private readonly ILogger logger;

        public SearchWantedTask(IJsonSerializer serializer, ILogger logger)
        {
            this.serializer = serializer;
            this.logger = logger;

            try
            {
                this.Configuration = Plugin.Instance.Configuration;
            }
            catch (Exception)
            {
                logger.Warn("Could not load configuration");
            }
        }

        public string Category
        {
            get { return "CouchPotato"; }
        }

        public string Key
        {
            get { return "CP_SearchWanted"; }
        }

        public string Description
        {
            get { return "Search for wanted movies"; }
        }

        public PluginConfiguration Configuration
        {
            get;
            set;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            const string startCmd = "movie.searcher.full_search";
            const string progressCmd = "movie.searcher.progress";

            string serverUrl = this.Configuration.ServerUrl;

            if (serverUrl.EndsWith("/"))
                serverUrl = serverUrl.Substring(0, serverUrl.Length - 1);

            string commandUrl = string.Join("/", serverUrl, "api", this.Configuration.ApiKey, startCmd);
            string progressUrl = string.Join("/", serverUrl, "api", this.Configuration.ApiKey, progressCmd);

            double progressCount = 0;
            using (HttpClient client = new HttpClient())
            {
                this.logger.Info($"Calling {commandUrl}");
                HttpResponseMessage response = await client.GetAsync(new Uri(commandUrl), cancellationToken);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    this.logger.Error($"Failed to call endpoint:{response.StatusCode}");
                    progress.Report(100);
                    return;
                }

                while (progressCount > -1 && progressCount < 100)
                {
                    TimeSpan delay = TimeSpan.FromSeconds(this.Configuration.DelayTimeInSeconds);
                    await Task.Delay(delay, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    progressCount = await this.executeProgressAsync(cancellationToken, client, progressUrl);
                    if (progressCount < 0)
                        throw new OperationCanceledException("Progress failure");
                }
            }

            progress.Report(100);
        }

        private async Task<double> executeProgressAsync(CancellationToken cancellationToken, HttpClient client, string progressUrl)
        {
            this.logger.Info($"Calling {progressUrl}");
            HttpResponseMessage response = await client.GetAsync(new Uri(progressUrl), cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
                return -1;

            string jsonResult = await response.Content.ReadAsStringAsync();

            ProgressResult result;
            try
            {
                result = this.serializer.DeserializeFromString<ProgressResult>(jsonResult);
            }
            catch (Exception ex)
            {
                if (jsonResult.Equals("{\"movie\": false}"))
                    return 100;
                this.logger.ErrorException("Failed to parse json", ex);
                return -1;
            }

            return (result.Movie.Total - result.Movie.To_Go) / result.Movie.Total * 100;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new TaskTriggerInfo[0];
        }

        public string Name
        {
            get { return "Search wanted"; }
        }
    }
}