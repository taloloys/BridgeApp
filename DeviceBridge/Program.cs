using System;
using Microsoft.Owin.Hosting;

namespace DeviceBridge
{
	internal static class Program
	{
		[STAThread]
		private static void Main()
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
						
						// Keep running until interrupted
						var resetEvent = new System.Threading.ManualResetEventSlim(false);
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
	}
}

