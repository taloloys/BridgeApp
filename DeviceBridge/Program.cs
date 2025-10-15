using System;
using System.Windows.Forms;
using Microsoft.Owin.Hosting;
using DeviceBridge.Services;

namespace DeviceBridge
{
	internal static class Program
	{
		[STAThread]
		private static void Main(string[] args)
		{
			// Initialize custom assembly resolver for Digital Persona SDK
			AssemblyResolver.Initialize();
			
			// Enable visual styles for WinForms BEFORE creating any controls/windows
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			

			// Check command line arguments
			if (args.Length > 0 && args[0].ToLower() == "help")
			{
				ShowHelp();
				return;
			}

 			// Default: run MainForm (starts minimized to tray per MainForm.cs)
			RunAsWinFormsApp();
		}

		private static void RunAsWinFormsApp()
		{
			try
			{
				// Run the main WinForms application
				Application.Run(new MainForm());
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error starting WinForms application: {ex.Message}", 
					"Device Bridge Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private static void RunAsConsole()
		{
			var ports = new[] { 18420, 18421, 18422, 18423, 18424 };

			foreach (var port in ports)
			{
				var url = $"http://127.0.0.1:{port}";
				try
				{
					using (WebApp.Start<Startup>(url))
					{
						Console.WriteLine($"Bridge running at {url}");
						Console.WriteLine("Press Ctrl+C to stop...");
						Console.WriteLine("Press 'T' to minimize to system tray...");

						// Keep running until interrupted
						var resetEvent = new System.Threading.ManualResetEventSlim(false);
						var keyWatcher = new System.Threading.Tasks.Task(() =>
						{
							while (!resetEvent.IsSet)
							{
								if (Console.KeyAvailable)
								{
									var key = Console.ReadKey(true);
									if (key.KeyChar == 't' || key.KeyChar == 'T')
									{
										Console.WriteLine("\nMinimizing to system tray...");
										MinimizeToSystemTray();
										return;
									}
								}
								System.Threading.Thread.Sleep(100);
							}
						});
						keyWatcher.Start();

						Console.CancelKeyPress += (sender, e) =>
						{
							e.Cancel = true;
							Console.WriteLine("\nShutting down...");
							resetEvent.Set();
						};

						resetEvent.Wait();
						break;
					}
				}
				catch (System.Net.HttpListenerException ex) when (ex.Message.Contains("conflicts with an existing registration"))
				{
					Console.WriteLine($"Port {port} is in use, trying next port...");
					continue;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Failed to start on port {port}: {ex.Message}");
					if (port == ports[ports.Length - 1]) // Last port
					{
						Console.WriteLine("All ports failed. Press any key to exit...");
						try { Console.ReadKey(); } catch { }
					}
					break;
				}
			}
		}
	

	private static void RunAsSystemTray()
		{
			try
			{
				// Start the system tray application
				using (var trayApp = new Services.SystemTrayBridge())
				{
					// Start the web server in a background thread
					System.Threading.Tasks.Task.Run(() => StartWebServer(trayApp));

					Console.WriteLine("Device Bridge running as system tray application");
					Console.WriteLine("Right-click the system tray icon to access options");
					Console.WriteLine("Press Ctrl+C to exit...");

					// Keep console alive
					var resetEvent = new System.Threading.ManualResetEventSlim(false);
					Console.CancelKeyPress += (sender, e) =>
					{
						e.Cancel = true;
						Console.WriteLine("\nShutting down system tray application...");
						resetEvent.Set();
					};

					// Run the system tray application
					System.Windows.Forms.Application.Run(trayApp);
					resetEvent.Wait();
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error starting system tray application: {ex.Message}");
				System.Windows.Forms.MessageBox.Show($"Failed to start system tray application: {ex.Message}",
					"Device Bridge Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
			}
		}

		private static void StartWebServer(Services.SystemTrayBridge trayApp)
		{
			var ports = new[] { 18420, 18421, 18422, 18423, 18424 };

			foreach (var port in ports)
			{
				var url = $"http://127.0.0.1:{port}";
				try
				{
					using (WebApp.Start<Startup>(url))
					{
						Console.WriteLine($"Web server started at {url}");
						Console.WriteLine("System tray application is ready");

						// Keep the web server running
						System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
					}
				}
				catch (System.Net.HttpListenerException ex) when (ex.Message.Contains("conflicts with an existing registration"))
				{
					Console.WriteLine($"Port {port} is in use, trying next port...");
					continue;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Failed to start web server on port {port}: {ex.Message}");
					if (port == ports[ports.Length - 1]) // Last port
					{
						Console.WriteLine("All ports failed for web server");
						return;
					}
				}
			}
		}

		private static void MinimizeToSystemTray()
		{
			try
			{
				// Hide the console window
				var consoleWindow = GetConsoleWindow();
				if (consoleWindow != IntPtr.Zero)
				{
					ShowWindow(consoleWindow, 0); // SW_HIDE
				}

				// Start system tray application
				using (var trayApp = new Services.SystemTrayBridge())
				{
					// Show notification
					trayApp.ShowNotification("Device Bridge minimized to system tray", "Right-click the tray icon for options");

					// Run the system tray application
					System.Windows.Forms.Application.Run(trayApp);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error minimizing to system tray: {ex.Message}");
				// Restore console window if error occurs
				var consoleWindow = GetConsoleWindow();
				if (consoleWindow != IntPtr.Zero)
				{
					ShowWindow(consoleWindow, 5); // SW_SHOW
				}
			}
		}

		private static void ShowHelp()
		{
			MessageBox.Show("Device Bridge - Fingerprint Service\n\n" +
				"Usage:\n" +
				"  DeviceBridge.exe          - Run as System Tray application (default)\n" +
				"  DeviceBridge.exe help     - Show this help\n\n" +
				"WinForms Mode:\n" +
				"  - Modern graphical user interface\n" +
				"  - Can minimize to system tray\n" +
				"  - User-friendly controls and status display\n" +
				"  - Web interface integration\n" +
				"  - Better integration with Windows",
				"Device Bridge Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		// Windows API for console window management
		[System.Runtime.InteropServices.DllImport("kernel32.dll")]
		private static extern IntPtr GetConsoleWindow();

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
	}
}
