using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;
using DeviceBridge.Services;

namespace DeviceBridge.Services
{
    public class FingerprintEnroll : DPFP.Capture.EventHandler, IDisposable
    {
        private readonly Capture capture;
	private readonly Enrollment enrollment;
	private readonly AutoResetEvent sampleEvent = new AutoResetEvent(false);
	private Template resultTemplate;
	private bool fingerDetected = false;
	// private double lastQuality = 0; // Removed unused field
	private readonly string sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
    private Form hiddenForm;
    private DateTime lastActivityUtc = DateTime.UtcNow;

	private void Log(string message)
	{
		System.Diagnostics.Debug.WriteLine($"[Enroll {sessionId}] {message}");
	}

	private SystemTrayBridge GetSystemTrayBridge()
	{
		try
		{
			// Get the current system tray bridge instance
			return SystemTrayBridge.Current;
		}
		catch
		{
			return null;
		}
	}

    private void TouchActivity()
    {
        lastActivityUtc = DateTime.UtcNow;
    }

    public event Action<int, string, string, int?> OnSampleProcessed;

	public FingerprintEnroll()
	{
		Log("ctor");
		
        // Create an off-screen tiny form to ensure a real HWND exists
        hiddenForm = new Form();
        hiddenForm.ShowInTaskbar = false;
        hiddenForm.FormBorderStyle = FormBorderStyle.None;
        hiddenForm.StartPosition = FormStartPosition.Manual;
        hiddenForm.Size = new System.Drawing.Size(1, 1);
        hiddenForm.Location = new System.Drawing.Point(-32000, -32000);
        hiddenForm.Opacity = 0.01;
        hiddenForm.Load += (s, e) => { var handle = hiddenForm.Handle; Log("Hidden form loaded"); };
        
        // Ensure the form is created and visible for proper message loop
        hiddenForm.Show();
        var _ = hiddenForm.Handle; // Force handle creation
        Application.DoEvents();
		
        try
        {
            var readers = new ReadersCollection();
            if (readers != null && readers.Count > 0)
            {
                var serial = readers[0].SerialNumber;
                Log($"Using reader: {serial}");
                capture = new Capture(serial);
            }
            else
            {
                Log("No readers found via ReadersCollection; falling back to default Capture()");
                capture = new Capture();
            }
        }
        catch (Exception ex)
        {
            Log($"ReadersCollection failed: {ex.Message}; falling back to default Capture()");
            capture = new Capture();
        }
        capture.EventHandler = this;
		enrollment = new Enrollment();
		
		// Note: Sensitivity adjustment not available in this DPFP version
	}

	public void Dispose()
	{
		try { capture?.StopCapture(); } catch { }
		capture?.Dispose();
		sampleEvent?.Dispose();
		hiddenForm?.Dispose();
	}

	public void Start()
	{
		try
		{
			Log("Starting fingerprint capture...");
			capture.StartCapture();
			Log("Fingerprint capture started successfully");
			// Note: some SDK versions don't expose Ready; ignore if unavailable
		}
		catch (DPFP.Error.SDKException sdkEx)
		{
			Log($"DPFP SDK Exception: {sdkEx.Message}");
			throw;
		}
	}

	public void Stop()
	{
		try { capture.StopCapture(); Log("StopCapture called"); } catch (Exception ex) { Log($"StopCapture error: {ex.Message}"); }
	}

	public bool IsComplete(out byte[] templateBytes)
	{
		templateBytes = null;
		if (resultTemplate != null)
		{
			using (var ms = new MemoryStream())
			{
				resultTemplate.Serialize(ms);
				templateBytes = ms.ToArray();
			}
			Log($"IsComplete: SUCCESS, bytes={templateBytes.Length}");
			return true;
		}
		return false;
	}

	public void OnComplete(object Capture, string ReaderSerialNumber, Sample sample)
	{
		try
		{
			Log("=== OnComplete START ===");
            if (!fingerDetected)
            {
                fingerDetected = true;
                OnSampleProcessed?.Invoke(10, "scanning", "Finger detected, initializing scan...", null);
            }

			var features = ExtractFeatures(sample, DataPurpose.Enrollment);
			Log($"Features extracted: {features != null}");

			if (features != null)
			{
                TouchActivity();
				enrollment.AddFeatures(features);
                var needed = enrollment.FeaturesNeeded;
				var status = enrollment.TemplateStatus;

				var progress = Math.Min(100, (4 - (int)needed) * 25);
                OnSampleProcessed?.Invoke(progress, "scanning", needed > 0 ? $"Lift and place finger again... ({needed} more)" : "Processing...", (int)needed);
				Log($"FeaturesNeeded={needed} Status={status} Progress={progress}");

				if (status == Enrollment.Status.Ready)
				{
                    OnSampleProcessed?.Invoke(85, "processing", "Processing template...", 0);
					resultTemplate = enrollment.Template;
					try { capture.StopCapture(); } catch { }
					sampleEvent.Set();
					Log("Template READY -> sampleEvent.Set()");
				}
				else if (status == Enrollment.Status.Failed)
				{
					enrollment.Clear();
					fingerDetected = false;
                    OnSampleProcessed?.Invoke(0, "waiting", "Enrollment failed. Please try again.", null);
					Log("Enrollment FAILED -> cleared");
				}
			}
			else
			{
                OnSampleProcessed?.Invoke(25, "scanning", "Poor quality scan, please try again...", null);
				Log("Feature extraction returned null (poor quality)");
			}
		}
		catch (Exception ex)
		{
			Log($"OnComplete error: {ex.Message}");
            OnSampleProcessed?.Invoke(0, "failed", "Fingerprint processing error occurred.", null);
		}
	}

	public void OnFingerTouch(object c, string s)
	{
		Log($"OnFingerTouch - fingerDetected={fingerDetected}");
        TouchActivity();
        if (!fingerDetected)
        {
            OnSampleProcessed?.Invoke(5, "scanning", "Finger detected on scanner...", null);
            Log("OnFingerTouch - finger detected, updating UI");
        }
	}

	public void OnFingerGone(object c, string s)
	{
		Log($"OnFingerGone - fingerDetected={fingerDetected}, status={enrollment.TemplateStatus}");
        if (fingerDetected && enrollment.TemplateStatus != Enrollment.Status.Ready)
        {
            OnSampleProcessed?.Invoke(0, "waiting", "Place your finger back on the scanner...", (int?)enrollment.FeaturesNeeded);
            Log("OnFingerGone - finger removed, prompting user");
        }
	}

	public void OnReaderConnect(object c, string s) { Log("OnReaderConnect"); }
    public void OnReaderDisconnect(object c, string s) { Log("OnReaderDisconnect"); }
    public void OnSampleQuality(object c, string s, CaptureFeedback f)
    {
        Log($"OnSampleQuality: {f}");
        if (f == CaptureFeedback.Good)
        {
            TouchActivity();
            OnSampleProcessed?.Invoke(50, "scanning", "Good quality detected", (int?)enrollment.FeaturesNeeded);
        }
    }

	private FeatureSet ExtractFeatures(Sample sample, DataPurpose purpose)
	{
		var extractor = new FeatureExtraction();
		var feedback = CaptureFeedback.None;
		var features = new FeatureSet();
		extractor.CreateFeatureSet(sample, purpose, ref feedback, ref features);
		return (feedback == CaptureFeedback.Good || feedback == CaptureFeedback.None) ? features : null;
	}

	public bool TryEnroll(int timeoutMs, out byte[] templateBytes)
	{
		return TryEnroll(timeoutMs, System.Threading.CancellationToken.None, out templateBytes);
	}

	public bool TryEnroll(int timeoutMs, System.Threading.CancellationToken cancellationToken, out byte[] templateBytes)
	{
		templateBytes = null;
		resultTemplate = null;
		fingerDetected = false;
		enrollment.Clear();
        TouchActivity();

		Log($"TryEnroll start timeoutMs={timeoutMs}");
		
        // Ensure the hidden form is visible and has focus for proper message loop
        try
        {
            hiddenForm.Show();
            var _ = hiddenForm.Handle; // force handle creation
            hiddenForm.BringToFront();
            hiddenForm.Activate();
            Application.DoEvents();
            
            // If we're running in system tray mode, try to get the system tray bridge's window
            var systemTrayBridge = GetSystemTrayBridge();
            if (systemTrayBridge != null)
            {
                systemTrayBridge.EnsureFocus();
            }
            // else: no external focus handling required
        }
        catch (Exception ex)
        {
            Log($"Form focus setup failed: {ex.Message}");
        }
		
        Start();
		try
		{
			var checkInterval = 50; // Faster polling
			while ((DateTime.UtcNow - lastActivityUtc).TotalMilliseconds < timeoutMs)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					Log("TryEnroll cancelled by token");
					OnSampleProcessed?.Invoke(0, "cancelled", "Enrollment cancelled", null);
					return false;
				}

				// Critical: Pump Windows messages for DPFP callbacks
                // Always pump on the message thread, even when app is not foreground
                try 
                { 
                    Application.DoEvents(); 
                    // Also try to maintain focus periodically
					var elapsedSinceActivityMs = (int)(DateTime.UtcNow - lastActivityUtc).TotalMilliseconds;
					if (elapsedSinceActivityMs % 2000 < checkInterval) // ~every 2s in inactivity window
                    {
                        try
                        {
                            hiddenForm.BringToFront();
                            hiddenForm.Activate();
                        }
                        catch { }
                    }
                } 
                catch (Exception ex) 
                { 
                    Log($"DoEvents failed: {ex.Message}");
                }
				
				if (sampleEvent.WaitOne(0))
				{
					if (resultTemplate != null)
					{
						using (var ms = new MemoryStream())
						{
							resultTemplate.Serialize(ms);
							templateBytes = ms.ToArray();
						}
						Log($"TryEnroll SUCCESS at bytes={templateBytes?.Length}");
						return true;
					}
				}
                try { Thread.Sleep(checkInterval); } catch { }
                var totalElapsed = (int)(DateTime.UtcNow - lastActivityUtc).TotalMilliseconds;
                if (totalElapsed % 1000 < checkInterval) Log($"waiting... inactive-window-elapsed={totalElapsed}ms");
			}
			Log("TryEnroll TIMEOUT");
			return false;
		}
		finally
		{
			Stop();
			try { hiddenForm.Hide(); } catch { }
			Log("TryEnroll end");
		}
	}
}
}
