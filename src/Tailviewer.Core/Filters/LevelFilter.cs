﻿using System.Collections.Generic;
using System.Linq;
using Tailviewer.BusinessLogic;
using Tailviewer.BusinessLogic.Filters;
using Tailviewer.BusinessLogic.LogFiles;

namespace Tailviewer.Core.Filters
{
	/// <summary>
	///     A filter that can be used to exclude entries of certain levels.
	/// </summary>
	public sealed class LevelFilter
		: ILogEntryFilter
	{
		private  readonly LevelFlags _level;

		/// <summary>
		///     Initializes this filter.
		/// </summary>
		/// <param name="level"></param>
		public LevelFilter(LevelFlags level)
		{
			_level = level;
		}

		/// <inheritdoc />
		public bool PassesFilter(IEnumerable<LogLine> logEntry)
		{
// ReSharper disable LoopCanBeConvertedToQuery
			foreach (LogLine logLine in logEntry)
// ReSharper restore LoopCanBeConvertedToQuery
			{
				if (PassesFilter(logLine))
					return true;
			}

			return false;
		}

		/// <inheritdoc />
		public bool PassesFilter(LogLine logLine)
		{
			if ((logLine.Level & _level) != 0)
				return true;

			if (logLine.Level != LevelFlags.None)
				return false;

			return true;
		}

		/// <inheritdoc />
		public List<LogLineMatch> Match(LogLine line)
		{
			return new List<LogLineMatch>();
		}

		/// <inheritdoc />
		public void Match(LogLine line, List<LogLineMatch> matches)
		{
			
		}

		/// <inheritdoc />
		public override string ToString()
		{
			var flags =
				new[] {LevelFlags.Debug, LevelFlags.Info, LevelFlags.Warning, LevelFlags.Error, LevelFlags.Fatal }
				.Where(m => _level.HasFlag(m))
				.ToList();

			if (flags.Count == 0)
			{
				return string.Format("level == {0}", LevelFlags.None);
			}
			if (flags.Count == 1)
			{
				return string.Format("level == {0}", flags[0]);
			}
			return string.Format("(level == {0})", string.Join(" || ", flags));
		}
	}
}