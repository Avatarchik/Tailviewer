﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using Tailviewer.BusinessLogic.LogFiles;

namespace Tailviewer.Core.Filters
{
	public sealed class RegexFilter
		: ILogEntryFilter
	{
		private readonly Regex _regex;

		public RegexFilter(string pattern, bool isCaseSensitive)
		{
			var options = RegexOptions.Compiled;
			if (isCaseSensitive)
				options |= RegexOptions.IgnoreCase;

			_regex = new Regex(pattern, options);
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
			if (_regex.IsMatch(logLine.Message))
				return true;

			return false;
		}

		/// <inheritdoc />
		public List<LogLineMatch> Match(LogLine line)
		{
			var ret = new List<LogLineMatch>();
			Match(line, ret);
			return ret;
		}

		/// <inheritdoc />
		public void Match(LogLine line, List<LogLineMatch> matches)
		{
			var regexMatches = _regex.Matches(line.Message);
			matches.Capacity += regexMatches.Count;
			for(int i = 0; i < regexMatches.Count; ++i)
			{
				matches.Add(new LogLineMatch(regexMatches[i]));
			}
		}
	}
}