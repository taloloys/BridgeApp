using System;
using System.Threading;
using System.Windows.Forms;

namespace DeviceBridge.Services
{
	public static class WinFormsPump
	{
		// Persistent STA thread with a hidden form to host a stable WinForms message loop.
		// DPFP SDK event callbacks are more reliable when capture/enrollment objects
		// are created and used on a single STA thread that owns a message pump.

		private static Thread _staThread;
		private static Control _invoker; // created on the STA thread
		private static readonly ManualResetEventSlim _ready = new ManualResetEventSlim(false);
		private static readonly object _initLock = new object();

		private static void EnsureStarted()
		{
			if (_invoker != null && _staThread != null) return;
			lock (_initLock)
			{
				if (_invoker != null && _staThread != null) return;
				_staThread = new Thread(() =>
				{
					// Create a hidden form/control to marshal work onto this thread
					using (var form = new Form())
					{
						form.ShowInTaskbar = false;
						form.WindowState = FormWindowState.Minimized;
						form.StartPosition = FormStartPosition.Manual;
						form.Location = new System.Drawing.Point(-2000, -2000);
						form.Load += (s, e) => { _invoker = form; _ready.Set(); };
						Application.Run(form);
					}
				})
				{
					IsBackground = true,
					Name = "DeviceBridge.WinFormsPump",
				};
				try { _staThread.SetApartmentState(ApartmentState.STA); } catch { }
				_staThread.Start();
				_ready.Wait();
			}
		}

		public static bool EnrollWithPump(Func<(bool ok, byte[] tpl)> work, out byte[] tpl)
		{
			EnsureStarted();
			byte[] tplLocal = null;
			var resultLocal = false;
			var done = new ManualResetEventSlim(false);

			// Execute the enrollment work on the STA/pump thread so DPFP callbacks
			// are raised on the same thread with a live message loop.
			_invoker.BeginInvoke(new MethodInvoker(() =>
			{
				try
				{
					var r = work();
					resultLocal = r.ok;
					tplLocal = r.tpl;
				}
				catch
				{
					// swallow; controller returns failure
				}
				finally
				{
					done.Set();
				}
			}));

			done.Wait();
			tpl = tplLocal;
			return resultLocal;
		}
	}
}


