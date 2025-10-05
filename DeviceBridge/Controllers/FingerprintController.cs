using System;
using System.Collections.Generic;
using System.Web.Http;
using DeviceBridge.Services;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Concurrent;

namespace DeviceBridge.Controllers
{
	[RoutePrefix("api/fingerprint")]
	public class FingerprintController : ApiController
	{
		private static readonly System.Threading.SemaphoreSlim _deviceLock = new System.Threading.SemaphoreSlim(1, 1);
		private static readonly ConcurrentDictionary<string, ProgressState> _progress = new ConcurrentDictionary<string, ProgressState>();

		private class ProgressState
		{
			public int Progress { get; set; }
			public string Phase { get; set; }
			public string Message { get; set; }
			public int? ScansLeft { get; set; }
			public byte[] Template { get; set; }
			public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
		}

		[HttpPost, Route("enroll/start")]
		public IHttpActionResult EnrollStart()
		{
			var sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
			_progress[sessionId] = new ProgressState { Progress = 0, Phase = "waiting", Message = "Initializing...", ScansLeft = null };

			try
			{
				// Ensure exclusive access to the device
				_deviceLock.Wait();

				ThreadPool.QueueUserWorkItem(state =>
				{
					try
					{
						using (var en = new FingerprintEnroll())
						{
							en.OnSampleProcessed += (p, phase, msg, left) =>
							{
								_progress.AddOrUpdate(sessionId,
									key => new ProgressState { Progress = p, Phase = phase, Message = msg, ScansLeft = left, UpdatedUtc = DateTime.UtcNow },
									(key, s) => { s.Progress = p; s.Phase = phase; s.Message = msg; s.ScansLeft = left; s.UpdatedUtc = DateTime.UtcNow; return s; });
							};

							var okEnroll = en.TryEnroll(30_000, out var templateBytes);
							if (okEnroll && templateBytes != null)
							{
								_progress.AddOrUpdate(sessionId,
									key => new ProgressState { Progress = 100, Phase = "done", Message = "Enrollment complete", ScansLeft = 0, Template = templateBytes, UpdatedUtc = DateTime.UtcNow },
									(key, s) => { s.Progress = 100; s.Phase = "done"; s.Message = "Enrollment complete"; s.ScansLeft = 0; s.Template = templateBytes; s.UpdatedUtc = DateTime.UtcNow; return s; });
							}
							else
							{
								_progress.AddOrUpdate(sessionId,
									key => new ProgressState { Progress = 0, Phase = "failed", Message = "Enrollment failed or timed out", ScansLeft = null, Template = null, UpdatedUtc = DateTime.UtcNow },
									(key, s) => { s.Progress = 0; s.Phase = "failed"; s.Message = "Enrollment failed or timed out"; s.ScansLeft = null; s.Template = null; s.UpdatedUtc = DateTime.UtcNow; return s; });
							}
						}
					}
					catch (Exception ex)
					{
						_progress.AddOrUpdate(sessionId,
							key => new ProgressState { Progress = 0, Phase = "failed", Message = ex.Message, ScansLeft = null, Template = null, UpdatedUtc = DateTime.UtcNow },
							(key, s) => { s.Progress = 0; s.Phase = "failed"; s.Message = ex.Message; s.ScansLeft = null; s.Template = null; s.UpdatedUtc = DateTime.UtcNow; return s; });
					}
					finally
					{
						try { _deviceLock.Release(); } catch { }
					}
				});

				return Ok(new { sessionId });
			}
			catch (Exception ex)
			{
				try { _deviceLock.Release(); } catch { }
				return BadRequest(ex.Message);
			}
		}

		[HttpGet, Route("enroll/progress/{sessionId}")]
		public IHttpActionResult EnrollProgress(string sessionId)
		{
			if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId required");
			if (_progress.TryGetValue(sessionId, out var st))
			{
				return Ok(new { progress = st.Progress, phase = st.Phase, message = st.Message, scansLeft = st.ScansLeft, done = st.Phase == "done", failed = st.Phase == "failed" });
			}
			return Ok(new { progress = 0, phase = "waiting", message = "No updates yet", scansLeft = (int?)null, done = false, failed = false });
		}

		[HttpPost, Route("enroll/finish/{sessionId}")]
		public IHttpActionResult EnrollFinish(string sessionId)
		{
			if (string.IsNullOrWhiteSpace(sessionId)) return BadRequest("sessionId required");
			if (_progress.TryGetValue(sessionId, out var st))
			{
				if (st.Template == null) return BadRequest("Enrollment not complete");
				var b64 = Convert.ToBase64String(st.Template);
				_progress.TryRemove(sessionId, out _);
				return Ok(new { template = b64, format = "DPFP" });
			}
			return BadRequest("Unknown sessionId");
		}

		// Removed old DoEvents loop; enrollment now runs under a real WinForms pump

		[HttpGet, Route("test/device")]
		public IHttpActionResult TestDevice()
		{
			try
			{
				if (FingerprintService.TryDetectReader(out var model, out var error))
					return Ok(new { available = true, model });
				return Ok(new { available = false, error });
			}
			catch (Exception ex)
			{
				return Ok(new { available = false, error = ex.Message });
			}
		}

		[HttpGet, Route("test/wpf-comparison")]
		public IHttpActionResult TestWpfComparison()
		{
			return Ok(new { 
				message = "To test if your reader works:",
				steps = new[] {
					"1. Close DeviceBridge completely",
					"2. Run your WPF FingerprintRegistrationPanel app", 
					"3. Try the fingerprint registration there",
					"4. If WPF works but bridge doesn't, it's a threading/STA issue",
					"5. If WPF also doesn't work, it's a reader/driver issue"
				},
				note = "The WPF app uses the same DPFP SDK but in a UI context - this will tell us if the issue is with the reader or the bridge implementation"
			});
		}

		[HttpGet, Route("test/events")]
		public IHttpActionResult TestEvents()
		{
			try
			{
				using (var enroller = new FingerprintEnroll())
				{
					enroller.Start();
					System.Diagnostics.Debug.WriteLine("=== EVENT TEST: Place finger on reader now ===");
					System.Threading.Thread.Sleep(10000); // Wait 10 seconds for finger placement
					enroller.Stop();
				}
				return Ok(new { message = "Event test completed. Check debug output for DPFP events." });
			}
			catch (Exception ex)
			{
				return BadRequest($"Event test failed: {ex.Message}");
			}
		}

		[HttpGet, Route("test/finger")]
		public IHttpActionResult TestFingerDetection()
		{
			try
			{
				using (var capture = new DPFP.Capture.Capture())
				{
					var events = new List<string>();
					capture.EventHandler = new SimpleEventHandler(events);
					
					// Try different capture initialization
					try 
					{
						// Some readers need this to enable finger detection
						capture.StartCapture();
						System.Diagnostics.Debug.WriteLine("=== FINGER DETECTION TEST: Touch the reader now ===");
						
						// Try multiple finger placements with different techniques
						for (int i = 0; i < 4; i++)
						{
							System.Diagnostics.Debug.WriteLine($"=== Attempt {i+1}: Place finger firmly and hold for 2 seconds ===");
							
							// Pump Windows messages during each attempt
							for (int j = 0; j < 20; j++) // 2 seconds = 20 * 100ms
							{
								System.Windows.Forms.Application.DoEvents();
								System.Threading.Thread.Sleep(100);
							}
						}
						
						capture.StopCapture();
					}
					catch (Exception capEx)
					{
						events.Add($"Capture error: {capEx.Message}");
						System.Diagnostics.Debug.WriteLine($"Capture error: {capEx.Message}");
					}
					
					return Ok(new { 
						message = "Finger detection test completed.", 
						events = events,
						count = events.Count,
						note = "Try pressing harder, different finger positions, or check if reader needs cleaning"
					});
				}
			}
			catch (Exception ex)
			{
				return BadRequest($"Finger detection test failed: {ex.Message}");
			}
		}

		[HttpGet, Route("test/system-tray")]
		public IHttpActionResult TestSystemTray()
		{
			var systemTrayActive = SystemTrayBridge.Current != null;
			
			return Ok(new { 
				message = "System Tray Application Test",
				systemTrayActive = systemTrayActive,
				instructions = new[] {
					"1. Stop the current console application",
					"2. Run: DeviceBridge.exe tray",
					"3. Look for the system tray icon",
					"4. Right-click the icon for options",
					"5. Test fingerprint enrollment while other apps are in focus"
				},
				benefits = new[] {
					"Works without application focus",
					"Always-on-top window for device events",
					"System tray access for easy management",
					"Better reliability for fingerprint operations",
					"Automatic focus maintenance every 30 seconds"
				},
				commands = new {
					consoleMode = "DeviceBridge.exe",
					trayMode = "DeviceBridge.exe tray",
					help = "DeviceBridge.exe help"
				},
				currentStatus = systemTrayActive ? "System tray bridge is active" : "System tray bridge is not active"
			});
		}

		[HttpGet, Route("test/focus")]
		public IHttpActionResult TestFocus()
		{
			try
			{
				var systemTrayBridge = SystemTrayBridge.Current;
				if (systemTrayBridge != null)
				{
					systemTrayBridge.EnsureFocus();
					return Ok(new { 
						message = "Focus ensured for fingerprint operations",
						systemTrayActive = true,
						note = "Hidden window has been brought to foreground and activated"
					});
				}
				else
				{
					return Ok(new { 
						message = "System tray bridge not active",
						systemTrayActive = false,
						note = "Run in tray mode for enhanced focus handling"
					});
				}
			}
			catch (Exception ex)
			{
				return BadRequest($"Focus test failed: {ex.Message}");
			}
		}
	}

	public class SimpleEventHandler : DPFP.Capture.EventHandler
	{
		private readonly List<string> _events;
		
		public SimpleEventHandler(List<string> events)
		{
			_events = events;
		}
		
		public void OnComplete(object Capture, string ReaderSerialNumber, DPFP.Sample Sample) 
		{ 
			_events.Add($"OnComplete: {ReaderSerialNumber}"); 
			System.Diagnostics.Debug.WriteLine($"[Simple] OnComplete: {ReaderSerialNumber}");
		}
		
		public void OnFingerTouch(object Capture, string ReaderSerialNumber) 
		{ 
			_events.Add($"OnFingerTouch: {ReaderSerialNumber}"); 
			System.Diagnostics.Debug.WriteLine($"[Simple] OnFingerTouch: {ReaderSerialNumber}");
		}
		
		public void OnFingerGone(object Capture, string ReaderSerialNumber) 
		{ 
			_events.Add($"OnFingerGone: {ReaderSerialNumber}"); 
			System.Diagnostics.Debug.WriteLine($"[Simple] OnFingerGone: {ReaderSerialNumber}");
		}
		
		public void OnReaderConnect(object Capture, string ReaderSerialNumber) 
		{ 
			_events.Add($"OnReaderConnect: {ReaderSerialNumber}"); 
			System.Diagnostics.Debug.WriteLine($"[Simple] OnReaderConnect: {ReaderSerialNumber}");
		}
		
		public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) 
		{ 
			_events.Add($"OnReaderDisconnect: {ReaderSerialNumber}"); 
			System.Diagnostics.Debug.WriteLine($"[Simple] OnReaderDisconnect: {ReaderSerialNumber}");
		}
		
		public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback) 
		{ 
			_events.Add($"OnSampleQuality: {CaptureFeedback}"); 
			System.Diagnostics.Debug.WriteLine($"[Simple] OnSampleQuality: {CaptureFeedback}");
		}
	}
}


