using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;

public class FingerprintEnroll : DPFP.Capture.EventHandler, IDisposable
{
    private readonly Capture capture;
	private readonly Enrollment enrollment;
	private readonly AutoResetEvent sampleEvent = new AutoResetEvent(false);
	private Template resultTemplate;
	private bool fingerDetected = false;
	private double lastQuality = 0;
	private readonly string sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
	private Form hiddenForm;

	private void Log(string message)
	{
		System.Diagnostics.Debug.WriteLine($"[Enroll {sessionId}] {message}");
	}

	public event Action<int, string, string> OnSampleProcessed;

	public FingerprintEnroll()
	{
		Log("ctor");
		
		// Create a hidden form to provide proper Windows message loop context
		hiddenForm = new Form();
		hiddenForm.WindowState = FormWindowState.Minimized;
		hiddenForm.ShowInTaskbar = false;
		hiddenForm.Visible = false;
		hiddenForm.Load += (s, e) => { Log("Hidden form loaded"); };
		
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
        catch
        {
            Log("ReadersCollection failed; falling back to default Capture()");
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
				OnSampleProcessed?.Invoke(10, "scanning", "Finger detected, initializing scan...");
			}

			var features = ExtractFeatures(sample, DataPurpose.Enrollment);
			Log($"Features extracted: {features != null}");

			if (features != null)
			{
				enrollment.AddFeatures(features);
				var needed = enrollment.FeaturesNeeded;
				var status = enrollment.TemplateStatus;

				var progress = Math.Min(100, (4 - (int)needed) * 25);
				lastQuality = 85.0;
				OnSampleProcessed?.Invoke(progress, "scanning", needed > 0 ? $"Lift and place finger again... ({needed} more)" : "Processing...");
				Log($"FeaturesNeeded={needed} Status={status} Progress={progress}");

				if (status == Enrollment.Status.Ready)
				{
					OnSampleProcessed?.Invoke(85, "processing", "Processing template...");
					resultTemplate = enrollment.Template;
					try { capture.StopCapture(); } catch { }
					sampleEvent.Set();
					Log("Template READY -> sampleEvent.Set()");
				}
				else if (status == Enrollment.Status.Failed)
				{
					enrollment.Clear();
					fingerDetected = false;
					OnSampleProcessed?.Invoke(0, "waiting", "Enrollment failed. Please try again.");
					Log("Enrollment FAILED -> cleared");
				}
			}
			else
			{
				OnSampleProcessed?.Invoke(25, "scanning", "Poor quality scan, please try again...");
				Log("Feature extraction returned null (poor quality)");
			}
		}
		catch (Exception ex)
		{
			Log($"OnComplete error: {ex.Message}");
			OnSampleProcessed?.Invoke(0, "failed", "Fingerprint processing error occurred.");
		}
	}

	public void OnFingerTouch(object c, string s)
	{
		Log($"OnFingerTouch - fingerDetected={fingerDetected}");
		if (!fingerDetected)
		{
			OnSampleProcessed?.Invoke(5, "scanning", "Finger detected on scanner...");
			Log("OnFingerTouch - finger detected, updating UI");
		}
	}

	public void OnFingerGone(object c, string s)
	{
		Log($"OnFingerGone - fingerDetected={fingerDetected}, status={enrollment.TemplateStatus}");
		if (fingerDetected && enrollment.TemplateStatus != Enrollment.Status.Ready)
		{
			OnSampleProcessed?.Invoke(0, "waiting", "Place your finger back on the scanner...");
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
            OnSampleProcessed?.Invoke(50, "scanning", "Good quality detected");
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
		templateBytes = null;
		resultTemplate = null;
		fingerDetected = false;
		enrollment.Clear();

		Log($"TryEnroll start timeoutMs={timeoutMs}");
		
		// Show the hidden form to establish message loop context
		hiddenForm.Show();
		Application.DoEvents(); // Process the form creation
		
		Start();
		try
		{
			var checkInterval = 50; // Faster polling
			var elapsed = 0;
			while (elapsed < timeoutMs)
			{
				// Critical: Pump Windows messages for DPFP callbacks
				Application.DoEvents();
				
				if (sampleEvent.WaitOne(0))
				{
					if (resultTemplate != null)
					{
						using (var ms = new MemoryStream())
						{
							resultTemplate.Serialize(ms);
							templateBytes = ms.ToArray();
						}
						Log($"TryEnroll SUCCESS at {elapsed}ms, bytes={templateBytes?.Length}");
						return true;
					}
				}
				Thread.Sleep(checkInterval);
				elapsed += checkInterval;
				if (elapsed % 1000 == 0) Log($"waiting... elapsed={elapsed}ms");
			}
			Log("TryEnroll TIMEOUT");
			return false;
		}
		finally
		{
			Stop();
			hiddenForm.Hide();
			Log("TryEnroll end");
		}
	}
}
