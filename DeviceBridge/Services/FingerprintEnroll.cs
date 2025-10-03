using System;
using System.IO;
using System.Threading;
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

	public event Action<int, string, string> OnSampleProcessed;

	public FingerprintEnroll()
	{
		capture = new Capture();
		capture.EventHandler = this;
		enrollment = new Enrollment();
	}

	public void Dispose()
	{
		try { capture?.StopCapture(); } catch { }
		capture?.Dispose();
		sampleEvent?.Dispose();
	}

	public void Start()
	{
		try
		{
			System.Diagnostics.Debug.WriteLine("Starting fingerprint capture...");
			capture.StartCapture();
			System.Diagnostics.Debug.WriteLine("Fingerprint capture started successfully");
		}
		catch (DPFP.Error.SDKException sdkEx)
		{
			System.Diagnostics.Debug.WriteLine($"DPFP SDK Exception: {sdkEx.Message}");
			throw;
		}
	}

	public void Stop()
	{
		try { capture.StopCapture(); } catch { }
	}

	public void OnComplete(object Capture, string ReaderSerialNumber, Sample sample)
	{
		try
		{
			System.Diagnostics.Debug.WriteLine("=== OnComplete START ===");
			if (!fingerDetected)
			{
				fingerDetected = true;
				OnSampleProcessed?.Invoke(10, "scanning", "Finger detected, initializing scan...");
			}

			var features = ExtractFeatures(sample, DataPurpose.Enrollment);
			System.Diagnostics.Debug.WriteLine($"Features extracted: {features != null}");

			if (features != null)
			{
				enrollment.AddFeatures(features);
				var needed = enrollment.FeaturesNeeded;
				var status = enrollment.TemplateStatus;

				var progress = Math.Min(100, (4 - (int)needed) * 25);
				lastQuality = 85.0;
				OnSampleProcessed?.Invoke(progress, "scanning", needed > 0 ? $"Lift and place finger again... ({needed} more)" : "Processing...");

				if (status == Enrollment.Status.Ready)
				{
					OnSampleProcessed?.Invoke(85, "processing", "Processing template...");
					resultTemplate = enrollment.Template;
					sampleEvent.Set();
				}
				else if (status == Enrollment.Status.Failed)
				{
					enrollment.Clear();
					fingerDetected = false;
					OnSampleProcessed?.Invoke(0, "waiting", "Enrollment failed. Please try again.");
				}
			}
			else
			{
				OnSampleProcessed?.Invoke(25, "scanning", "Poor quality scan, please try again...");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"OnComplete error: {ex.Message}");
			OnSampleProcessed?.Invoke(0, "failed", "Fingerprint processing error occurred.");
		}
	}

	public void OnFingerTouch(object c, string s)
	{
		if (!fingerDetected)
		{
			OnSampleProcessed?.Invoke(5, "scanning", "Finger detected on scanner...");
		}
	}

	public void OnFingerGone(object c, string s)
	{
		if (fingerDetected && enrollment.TemplateStatus != Enrollment.Status.Ready)
		{
			OnSampleProcessed?.Invoke(0, "waiting", "Place your finger back on the scanner...");
		}
	}

	public void OnReaderConnect(object c, string s) { }
	public void OnReaderDisconnect(object c, string s) { }
	public void OnSampleQuality(object c, string s, CaptureFeedback f) { }

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

		Start();
		try
		{
			var checkInterval = 100;
			var elapsed = 0;
			while (elapsed < timeoutMs)
			{
				if (sampleEvent.WaitOne(checkInterval))
				{
					if (resultTemplate != null)
					{
						using (var ms = new MemoryStream())
						{
							resultTemplate.Serialize(ms);
							templateBytes = ms.ToArray();
						}
						return true;
					}
				}
				elapsed += checkInterval;
			}
			return false;
		}
		finally
		{
			Stop();
		}
	}
}
