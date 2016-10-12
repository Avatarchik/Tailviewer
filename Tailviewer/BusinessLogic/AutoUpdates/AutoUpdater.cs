﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Tailviewer.Settings;
using log4net;

namespace Tailviewer.BusinessLogic.AutoUpdates
{
	internal sealed class AutoUpdater
		: IDisposable, IAutoUpdater
	{
		private const string Server = "https://kittyfisto.github.io";
		private const string VersionFile = "Tailviewer/downloads/version.xml";

		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public static readonly Version CurrentAppVersion;

		private readonly object _syncRoot;
		private AutoUpdateSettings _settings;
		private VersionInfo _latestVersion;
		private readonly List<Action<VersionInfo>> _latestVersionChanged;

		static AutoUpdater()
		{
			try
			{
				CurrentAppVersion = Assembly.GetCallingAssembly().GetName().Version;
			}
			catch (Exception e)
			{
				Log.WarnFormat("Unable to read the current version: {0}", e);
			}
		}

		public AutoUpdater(AutoUpdateSettings settings)
		{
			_syncRoot = new object();
			_settings = settings;
			_latestVersionChanged = new List<Action<VersionInfo>>();
		}

		public void CheckForUpdatesAsync()
		{
			var updateTask = Task<VersionInfo>.Factory.StartNew(QueryNewestVersions);
			updateTask.ContinueWith(OnVersionChecked);
		}

		public event Action<VersionInfo> LatestVersionChanged
		{
			add
			{
				lock (_syncRoot)
				{
					_latestVersionChanged.Add(value);
					if (_latestVersion != null)
					{
						value(_latestVersion);
					}
				}
			}
			remove
			{
				lock (_syncRoot)
				{
					_latestVersionChanged.Remove(value);
				}
			}
		}

		private void OnVersionChecked(Task<VersionInfo> task)
		{
			try
			{
				VersionInfo latest = task.Result;
				LatestVersion = latest;
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Caught unexpected exception while querying newest version: {0}", e);
			}
		}

		public Version AppVersion
		{
			get { return CurrentAppVersion; }
		}

		public VersionInfo LatestVersion
		{
			get { return _latestVersion; }
			private set
			{
				lock (_syncRoot)
				{
					if (value == _latestVersion)
						return;

					_latestVersion = value;
					foreach (var fn in _latestVersionChanged)
					{
						fn(value);
					}
				}
			}
		}

		public void Dispose()
		{
		}

		internal static Uri BuildVersionCheckUri()
		{
			string address = string.Format("{0}/{1}",
			                               Server,
			                               VersionFile);
			var uri = new Uri(address);
			return uri;
		}

		private VersionInfo QueryNewestVersions()
		{
			try
			{
				using (var client = new WebClient())
				{
					client.UseDefaultCredentials = true;
					var proxy = WebRequest.GetSystemWebProxy();
					var credentials = _settings.GetProxyCredentials();
					if (credentials != null)
						proxy.Credentials = credentials;
					client.Proxy = proxy;

					Uri uri = BuildVersionCheckUri();

					Log.DebugFormat("Looking for newest version on '{0}", uri);
					byte[] data = client.DownloadData(uri);

					Log.DebugFormat("Parsing response ({0} bytes)",
					                data != null ? data.Length.ToString(CultureInfo.InvariantCulture) : "null");

					VersionInfo versions;
					Parse(data, out versions);
					Log.InfoFormat("Most recent versions: {0}", versions);
					return versions;
				}
			}
			catch (WebException e)
			{
				Log.WarnFormat("Unable to query the newest version: {0}", e);
				return null;
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Caught unexpected exception while querying newest version: {0}", e);
				return null;
			}
		}

		internal static void Parse(byte[] data, out VersionInfo latestVersions)
		{
			latestVersions = new VersionInfo(null, null, null, null);

			using (var stream = new MemoryStream(data))
			using (XmlReader reader = XmlReader.Create(stream))
			{
				while (reader.Read())
				{
					switch (reader.Name)
					{
						case "versions":
							ReadVersions(reader.ReadSubtree(), out latestVersions);
							break;
					}
				}
			}
		}

		private static void ReadVersions(XmlReader versions, out VersionInfo latestVersions)
		{
			Version beta = null;
			Uri betaAddress = null;
			Version stable = null;
			Uri stableAddress = null;

			while (versions.Read())
			{
				switch (versions.Name)
				{
					case "stable":
						stable = ReadVersion(versions, out stableAddress);
						break;

					case "beta":
						beta = ReadVersion(versions, out betaAddress);
						break;
				}
			}

			latestVersions = new VersionInfo(beta, betaAddress, stable, stableAddress);
		}

		private static Version ReadVersion(XmlReader versions, out Uri address)
		{
			var path = versions.GetAttribute("path");
			var fullPath = string.Format("{0}/Tailviewer/downloads/{1}", Server, path);
			if (!Uri.TryCreate(fullPath, UriKind.Absolute, out address))
				return null;

			int major;
			int minor;
			int build;
			if (!int.TryParse(versions.GetAttribute("major"), NumberStyles.Integer, CultureInfo.InvariantCulture, out major))
				return null;
			if (!int.TryParse(versions.GetAttribute("minor"), NumberStyles.Integer, CultureInfo.InvariantCulture, out minor))
				return null;
			if (!int.TryParse(versions.GetAttribute("build"), NumberStyles.Integer, CultureInfo.InvariantCulture, out build))
				return null;

			return new Version(major, minor, build);
		}
	}
}