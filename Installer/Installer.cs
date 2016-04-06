﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Installer.Exceptions;
using Metrolib;

namespace Installer
{
	internal sealed class Installer : IDisposable
	{
		private readonly Assembly _assembly;
		private readonly List<string> _files;
		private readonly Size _installationSize;
		private readonly string _prefix;
		private Size _installedSize;
		private double _progress;

		public Installer()
		{
			_assembly = Assembly.GetExecutingAssembly();
			_prefix = string.Format("{0}.InstallationFiles.", _assembly.GetName().Name);
			string[] allFiles = _assembly.GetManifestResourceNames();
			_files = allFiles.Where(x => x.Contains(_prefix)).ToList();
			_installationSize = _files.Aggregate(Size.Zero, (size, fileName) => size + Filesize(fileName));
		}

		public Size InstallationSize
		{
			get { return _installationSize; }
		}

		public double Progress
		{
			get { return _progress; }
		}

		public void Dispose()
		{
		}

		private Size Filesize(string fileName)
		{
			using (Stream stream = _assembly.GetManifestResourceStream(fileName))
			{
				if (stream == null)
					return Size.Zero;

				return Size.FromBytes(stream.Length);
			}
		}

		public void Run(string installationPath)
		{
			DateTime start = DateTime.Now;

			try
			{
				RemovePreviousInstallation(installationPath);
				EnsureInstallationPath(installationPath);
				InstallNewFiles(installationPath);
			}
			finally
			{
				DateTime end = DateTime.Now;
				TimeSpan elapsed = end - start;
				TimeSpan remaining = TimeSpan.FromMilliseconds(500) - elapsed;
				if (remaining > TimeSpan.Zero)
				{
					Thread.Sleep(remaining);
				}
			}
		}

		private void RemovePreviousInstallation(string installationPath)
		{
			foreach (var file in _files)
			{
				var fullFilePath = DestFilePath(installationPath, file);
				DeleteFile(fullFilePath);
			}
		}

		private void DeleteFile(string filePath)
		{
			var name = Path.GetFileName(filePath);
			var dir = Path.GetDirectoryName(filePath);

			try
			{
				File.Delete(filePath);
			}
			catch (Exception e)
			{
				throw new DeleteFileException(name, dir, e);
			}
		}

		private void EnsureInstallationPath(string installationPath)
		{
			Directory.CreateDirectory(installationPath);
		}

		private void InstallNewFiles(string installationPath)
		{
			foreach (string file in _files)
			{
				var destFilePath = DestFilePath(installationPath, file);
				CopyFile(destFilePath, file);
			}
		}

		private string DestFilePath(string installationPath, string file)
		{
			string fileName = file.Substring(_prefix.Length);
			string destFilePath = Path.Combine(installationPath, fileName);
			return destFilePath;
		}

		private void CopyFile(string destFilePath, string sourceFilePath)
		{
			var directory = Path.GetDirectoryName(destFilePath);
			var fileName = Path.GetFileName(destFilePath);

			try
			{

				using (var dest = new FileStream(destFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				using (Stream source = _assembly.GetManifestResourceStream(sourceFilePath))
				{
					const int size = 4096;
					var buffer = new byte[size];

					int read;
					while ((read = source.Read(buffer, 0, size)) > 0)
					{
						dest.Write(buffer, 0, read);
						_installedSize += Size.FromBytes(read);
						_progress = _installedSize / _installationSize;
					}
				}
			}
			catch (Exception e)
			{
				throw new CopyFileException(fileName, directory, e);
			}
		}
	}
}