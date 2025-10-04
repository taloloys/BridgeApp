using System;
using System.Collections.Generic;
using System.Web.Http;
using DeviceBridge.Services;
using System.Threading;
using System.Windows.Forms;

namespace DeviceBridge.Controllers
{
	[RoutePrefix("api/fingerprint")]
	public class FingerprintController : ApiController
	{
		private static readonly System.Threading.SemaphoreSlim _deviceLock = new System.Threading.SemaphoreSlim(1, 1);

		[HttpPost, Route("enroll")]
		public IHttpActionResult Enroll()
		{
			byte[] tpl = null;
			bool ok = false;
			Exception workerEx = null;
			var corrId = Guid.NewGuid().ToString("N").Substring(0, 8);
			System.Diagnostics.Debug.WriteLine($"[Enroll {corrId}] start");

			try
			{
				// Ensure exclusive access to the device
				_deviceLock.Wait();

				var t = new Thread(() =>
				{
					try
					{
						// Create a proper Windows message loop in STA thread
						using (var enroller = new FingerprintEnroll())
						{
							// Run enrollment with message loop
							ok = RunEnrollmentWithMessageLoop(enroller, 30_000, out tpl);
						}
					}
					catch (Exception ex)
					{
						workerEx = ex;
					}
				});
				try { t.SetApartmentState(ApartmentState.STA); } catch { }
				t.IsBackground = true;
				t.Start();
				t.Join();

				if (workerEx != null) throw workerEx;
				if (!ok || tpl == null)
				{
					System.Diagnostics.Debug.WriteLine($"[Enroll {corrId}] failed (timeout or null template)");
					return BadRequest("Enrollment timed out or failed.");
				}
				var b64 = Convert.ToBase64String(tpl);
				System.Diagnostics.Debug.WriteLine($"[Enroll {corrId}] success bytes={tpl.Length}");
				return Ok(new { template = b64, format = "DPFP", quality = (int?)null });
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[Enroll {corrId}] exception: {ex.Message}");
				return BadRequest(ex.Message);
			}
			finally
			{
				try { _deviceLock.Release(); } catch { }
				System.Diagnostics.Debug.WriteLine($"[Enroll {corrId}] end");
			}
		}

		private bool RunEnrollmentWithMessageLoop(FingerprintEnroll enroller, int timeoutMs, out byte[] templateBytes)
		{
			templateBytes = null;
			var result = false;
			var startTime = DateTime.Now;
			
			// Start enrollment
			enroller.Start();
			
			try
			{
				// Run a proper Windows message loop
				while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
				{
					// Process Windows messages
					Application.DoEvents();
					
					// Check if enrollment completed
					if (enroller.IsComplete(out templateBytes))
					{
						result = true;
						break;
					}
					
					// Small delay to prevent 100% CPU
					Thread.Sleep(10);
				}
			}
			finally
			{
				enroller.Stop();
			}
			
			return result;
		}

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


