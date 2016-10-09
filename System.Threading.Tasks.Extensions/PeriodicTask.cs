﻿using System.Reflection;
using log4net;

namespace System.Threading.Tasks
{
	internal sealed class PeriodicTask
		: IPeriodicTask
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		/// <summary>
		///     The time a task has to wait to be executed the next time.
		/// </summary>
		public static readonly TimeSpan DefaultWaitTime = TimeSpan.FromMilliseconds(10);

		private readonly Func<TimeSpan> _callback;
		private readonly long _id;
		private readonly string _name;
		private readonly TimeSpan _defaultWaitTime;
		private DateTime _lastInvocation;
		private TimeSpan _nextWaitTime;
		private int _numFailures;

		public PeriodicTask(long id, Action callback, TimeSpan minimumWaitTime, string name = null)
			: this(id, () =>
				{
					callback();
					return minimumWaitTime;
				}, name)
		{
			if (callback == null)
				throw new ArgumentNullException("callback");

			_defaultWaitTime = minimumWaitTime;
		}

		public PeriodicTask(long id, Func<TimeSpan> callback, string name = null)
		{
			if (callback == null)
				throw new ArgumentNullException("callback");

			_id = id;
			_callback = callback;
			_name = name;
			_defaultWaitTime = DefaultWaitTime;
		}

		public DateTime LastInvocation
		{
			get { return _lastInvocation; }
		}

		/// <summary>
		/// </summary>
		public bool IsRemoved { get; set; }

		public long Id
		{
			get { return _id; }
		}

		public int NumFailures
		{
			get { return _numFailures; }
		}

		public TimeSpan NextWaitTime
		{
			get { return _nextWaitTime; }
		}

		public string Name
		{
			get { return _name; }
		}

		/// <summary>
		///     Tests if this task should run now.
		/// </summary>
		/// <param name="now"></param>
		/// <param name="remainingWaitTime"></param>
		/// <returns></returns>
		public bool ShouldRun(DateTime now, out TimeSpan remainingWaitTime)
		{
			remainingWaitTime = now - _lastInvocation;
			return remainingWaitTime >= _nextWaitTime;
		}

		public override string ToString()
		{
			return string.Format("#{0} ({1})", _id, _name);
		}

		/// <summary>
		///     Executes this task.
		/// </summary>
		/// <returns>The time that shall ellapse until the task should be scheduled next.</returns>
		public void Run()
		{
			try
			{
				_nextWaitTime = _callback();
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Caught unexpected exception: {0}", e);
				++_numFailures;
				_nextWaitTime = _defaultWaitTime;
			}
			finally
			{
				_lastInvocation = DateTime.Now;
			}
		}
	}
}