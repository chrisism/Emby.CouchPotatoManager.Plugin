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

        private int errorCount = 0;

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
                logger.Warn("[CP Search] Could not load configuration");
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
            this.errorCount = 0;

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
                this.logger.Info($"[CP Search] Calling {commandUrl}");
                HttpResponseMessage response = await client.GetAsync(new Uri(commandUrl), cancellationToken);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    this.logger.Error($"[CP Search] Failed to call endpoint:{response.StatusCode}");
                    progress.Report(100);
                    return;
                }

                while (progressCount > -1 && progressCount < 100)
                {
                    TimeSpan delay = TimeSpan.FromSeconds(this.Configuration.DelayTimeInSeconds);
                    await Task.Delay(delay, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        progressCount = await this.executeProgressAsync(cancellationToken, client, progressUrl);
                        this.logger.Debug($"[CP Search] Called {progressUrl}. Returned progress of {progressCount}");
                    }
                    catch (Exception ex)
                    {
                        this.errorCount++;
                        this.logger.ErrorException($"[CP Search] Called {progressUrl}. Error while calling.", ex);
                    }

                    if (progressCount < 0 || this.errorCount > 10)
                        throw new OperationCanceledException("Progress failure");
                }
            }

            progress.Report(100);
        }

        private async Task<double> executeProgressAsync(CancellationToken cancellationToken, HttpClient client, string progressUrl)
        {   
            HttpResponseMessage response = await client.GetAsync(new Uri(progressUrl), cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
                return -1;

            string jsonResult = await response.Content.ReadAsStringAsync();

            if (jsonResult != null && jsonResult.Equals("{\"movie\": false}"))
                return 100;

            ProgressResult result;
            try
            {
                result = this.serializer.DeserializeFromString<ProgressResult>(jsonResult);
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("[CP Search] Failed to parse json", ex);
                return -1;
            }

            if (result == null)
            {
                this.logger.Error($"[CP Search] Return obj is NULL. Actual content: {jsonResult}");
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