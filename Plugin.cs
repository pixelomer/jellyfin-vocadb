using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VocaDB {
	class Plugin : BasePlugin<BasePluginConfiguration> {
		public const string ProviderName = "VocaDB";
		public const string ProviderId = "vocadb";
		public Plugin(
			IApplicationPaths applicationPaths,
			IXmlSerializer xmlSerializer,
			ILogger logger,
			IJsonSerializer jsonSerializer) : base(applicationPaths, xmlSerializer)
		{
			logger.Log(LogLevel.Information, "VocaDB initializing...");
		}
		public override string Name => "VocaDB";
		public override Guid Id => Guid.Parse("04e791d9-f27c-47dc-86a0-4ca4f60e937d");
	}
}