using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeviceBridge.Services
{
	public static class FocusHost
	{
		private static Form _host;
		private static readonly object _lock = new object();
		private static bool _inited;

		public static void Initialize()
		{
			if (_inited) return;
			lock (_lock)
			{
				if (_inited) return;
				_host = new Form
				{
					Text = "DeviceBridge FocusHost",
					ShowInTaskbar = false,
					FormBorderStyle = FormBorderStyle.None,
					Size = new Size(1, 1),
					StartPosition = FormStartPosition.Manual,
					Location = new Point(-2000, -2000),
					Opacity = 0.01,
					TopMost = true
				};
				_host.Load += (s, e) =>
				{
					try
					{
						_host.Visible = true;
						_host.TopMost = true;
						_host.BringToFront();
						_host.Activate();
					}
					catch { }
				};
				try { _host.Show(); } catch { }
				try { Application.DoEvents(); } catch { }
				_inited = true;
			}
		}

		public static void EnsureFocus()
		{
			try
			{
				if (_host == null || _host.IsDisposed) Initialize();
				_host.Visible = true;
				_host.TopMost = true;
				_host.BringToFront();
				_host.Activate();
			}
			catch { }
		}
	}
}


