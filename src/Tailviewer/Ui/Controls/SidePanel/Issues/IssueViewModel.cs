﻿using System;
using System.Windows.Input;
using System.Windows.Media;
using Metrolib;
using Tailviewer.BusinessLogic;
using Tailviewer.BusinessLogic.Plugins.Issues;

namespace Tailviewer.Ui.Controls.SidePanel.Issues
{
	internal sealed class IssueViewModel
	{
		private readonly ILogFileIssue _issue;
		private readonly Action<LogLineIndex> _goto;

		public IssueViewModel(ILogFileIssue issue, Action<LogLineIndex> @goto)
		{
			_issue = issue;
			_goto = @goto;
		}

		public ICommand GoToCommand => new DelegateCommand2(() => _goto(_issue.OriginalLineIndex));
		public Geometry Icon => Icons.Alert;
		public LogLineIndex Line => _issue.OriginalLineIndex;
		public string Summary => _issue.Summary;
		public string Description => _issue.Description;
		public DateTime? Timestamp => _issue.Timestamp;
		public Severity Severity => _issue.Severity;
		public ILogFileIssue Issue => _issue;
	}
}