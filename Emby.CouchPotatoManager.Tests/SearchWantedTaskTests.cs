using System;
using System.Threading;
using Emby.CouchPotatoManager.Plugin;
using Emby.CouchPotatoManager.Plugin.Domain;
using Emby.CouchPotatoManager.Plugin.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine.ClientProtocol;
using Moq;
using Xunit;

namespace Emby.CouchPotatoManager.Tests
{
    public class SearchWantedTaskTests
    {
        private SearchWantedTask target;
        private PluginConfiguration configuration;

        public SearchWantedTaskTests()
        {
            Mock<IApplicationPaths> paths = new Mock<IApplicationPaths>();

            var serializer = new Serializer();
            var plugin = new Plugin.Plugin(paths.Object, null);
            var logger = new ConsoleLogger();
            
            this.target = new SearchWantedTask(serializer, logger);
        }

        [Fact]
        public async void Executing_task_is_succesfull()
        {
            // arrange
            var token = new CancellationToken();
            var progress = new Progress<double>();
            
            this.configuration = new PluginConfiguration
            {
                ApiKey = "1964c20b679940c78500c2e14df473ec",
                DelayTimeInSeconds = 2,
                ServerUrl = "http://192.168.0.2:5050"
            };
            this.target.Configuration = this.configuration;

            // act
            await this.target.Execute(token, progress);

            // assert
        }

        [Fact]
        public void Count_tests()
        {
            var result = new ProgressResult
            {
                Movie = new MovieProgress
                {
                    To_Go = 47,
                    Total = 71
                }

            };

            var actual = (result.Movie.Total - result.Movie.To_Go) / result.Movie.Total * 100;
            Assert.NotEqual(0, actual);
        }
    }
}
