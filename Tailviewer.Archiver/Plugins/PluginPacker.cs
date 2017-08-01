﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Tailviewer.Archiver.PEHeader;

namespace Tailviewer.Archiver.Plugins
{
	/// <summary>
	///     Responsible for creating a plugin package which can then be dropped into Tailviewer's plugin folder.
	/// </summary>
	/// <remarks>
	///     Plugin authors do not need to this class directly, archiver.exe should be more convenient to use in many cases.
	/// </remarks>
	public sealed class PluginPacker
		: IDisposable
	{
		private readonly ZipArchive _archive;
		private readonly PluginPackageIndex _index;
		private bool _disposed;

		private PluginPacker(ZipArchive archive)
		{
			if (archive == null)
				throw new ArgumentNullException(nameof(archive));

			_archive = archive;
			_index = new PluginPackageIndex
			{
				Assemblies = new List<AssemblyDescription>()
			};
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (!_disposed)
			{
				StoreIndex();
				_archive.Dispose();
				_disposed = true;
			}
		}

		private void StoreIndex()
		{
			using (var stream = new MemoryStream())
			{
				using (var writer = new StreamWriter(stream, Encoding.UTF8, 4086, true))
				{
					var serializer = new XmlSerializer(typeof(PluginPackageIndex));
					serializer.Serialize(writer, _index);
				}

				stream.Position = 0;
				AddFile(PluginArchive.IndexEntryName, stream);
			}
		}

		/// <summary>
		///     Adds a Tailviewer plugin (which is a .NET assembly with special content) to the plugin archive.
		///     There can be only one plugin assembly per archive.
		/// </summary>
		/// <param name="pluginFilePath"></param>
		public void AddPluginAssembly(string pluginFilePath)
		{
			var assembly = AddAssembly(PluginArchive.PluginAssemblyEntryName, pluginFilePath);
			var assemblyLoader = new PluginAssemblyLoader();
			var description = assemblyLoader.ReflectPlugin(assembly);
			UpdateIndex(description);
		}
		
		/// <summary>
		///     Adds a new file to the  plugin package.
		/// </summary>
		/// <param name="entryName"></param>
		/// <param name="fileName"></param>
		public void AddFile(string entryName, string fileName)
		{
			using (var stream = File.OpenRead(fileName))
			{
				AddFile(entryName, stream);
			}
		}

		/// <summary>
		///     Adds a new file to the plugin package.
		/// </summary>
		/// <param name="entryName"></param>
		/// <param name="content"></param>
		public void AddFile(string entryName, Stream content)
		{
			PeHeaderReader reader;
			PeHeaderReader.TryReadFrom(content, out reader, leaveOpen: true);
			content.Position = 0;
			if (reader != null)
			{
				if (reader.IsClrAssembly)
				{
					AddAssembly(entryName, content);
				}
				else
				{
					// TODO: Mark this as a pe file in the index
					AddFileRaw(entryName, content);
				}
			}
			else
			{
				AddFileRaw(entryName, content);
			}
		}

		/// <summary>
		///     Adds a .NET assembly to the plugin archive.
		/// </summary>
		/// <param name="entryName">The relative name of the resulting file in the archive</param>
		/// <param name="content"></param>
		private Assembly AddAssembly(string entryName, Stream content)
		{
			byte[] rawAssembly;
			var assembly = LoadAssemblyFrom(content, out rawAssembly);
			var assemblyDescription = AssemblyDescription.FromAssembly(assembly);
			assemblyDescription.EntryName = entryName;
			AddFile(entryName, rawAssembly);
			_index.Assemblies.Add(assemblyDescription);
			return assembly;
		}

		/// <summary>
		///     Adds a .NET assembly to the plugin archive.
		/// </summary>
		/// <param name="entryName">The relative name of the resulting file in the archive</param>
		/// <param name="assemblyFileName"></param>
		private Assembly AddAssembly(string entryName, string assemblyFileName)
		{
			var assembly = Assembly.LoadFrom(assemblyFileName);
			var assemblyDescription = AssemblyDescription.FromAssembly(assembly);
			assemblyDescription.EntryName = entryName;
			AddFile(entryName, assemblyFileName);
			_index.Assemblies.Add(assemblyDescription);
			return assembly;
		}

		private void AddFileRaw(string entryName, Stream content)
		{
			var entry = _archive.CreateEntry(entryName, CompressionLevel.NoCompression);
			using (var stream = entry.Open())
			using (var writer = new BinaryWriter(stream))
			{
				var buffer = new byte[4096];
				int read;
				while ((read = content.Read(buffer, 0, buffer.Length)) > 0)
					writer.Write(buffer, 0, read);
			}
		}

		/// <summary>
		///     Adds a new file to the  plugin package.
		/// </summary>
		/// <param name="entryName"></param>
		/// <param name="content"></param>
		public void AddFile(string entryName, byte[] content)
		{
			var entry = _archive.CreateEntry(entryName, CompressionLevel.NoCompression);
			using (var stream = entry.Open())
			{
				stream.Write(content, 0, content.Length);
			}
		}

		private void UpdateIndex(IPluginDescription description)
		{
			_index.PluginDescription = description.Description;
			_index.PluginAuthor = description.Author;
			_index.PluginWebsite = description.Website != null ? description.Website.ToString() : null;
			_index.ImplementedPluginInterfaces = new List<PluginInterfaceImplementation>();
			foreach (var pair in description.Plugins)
			{
				_index.ImplementedPluginInterfaces.Add(new PluginInterfaceImplementation
				{
					InterfaceTypename = pair.Key.FullName,
					ImplementationTypename = pair.Value
				});
			}
		}

		private Assembly LoadAssemblyFrom(Stream assemblyContent, out byte[] rawAssembly)
		{
			rawAssembly = new byte[assemblyContent.Length - assemblyContent.Position];
			int offset = 0;
			int toRead;
			while ((toRead = Math.Min(4096, rawAssembly.Length - offset)) > 0)
			{
				offset += assemblyContent.Read(rawAssembly, offset, toRead);
			}
			return Assembly.Load(rawAssembly);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fname"></param>
		/// <returns></returns>
		public static PluginPacker Create(string fname)
		{
			return new PluginPacker(ZipFile.Open(fname, ZipArchiveMode.Create, Encoding.UTF8));
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="leaveOpen"></param>
		/// <returns></returns>
		public static PluginPacker Create(Stream stream, bool leaveOpen = false)
		{
			return new PluginPacker(new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen, Encoding.UTF8));
		}
	}
}