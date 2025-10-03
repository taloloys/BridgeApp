using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeviceBridge.Models;

namespace DeviceBridge.Services
{
	public class EnrollmentSessionManager
	{
		private static readonly EnrollmentSessionManager _instance = new EnrollmentSessionManager();
		public static EnrollmentSessionManager Instance => _instance;

		private readonly ConcurrentDictionary<string, EnrollmentSession> _sessions = new ConcurrentDictionary<string, EnrollmentSession>();
		private readonly Timer _cleanupTimer;
		private const int SESSION_TIMEOUT_SECONDS = 60;
		private static readonly System.Threading.SemaphoreSlim _deviceLock = new System.Threading.SemaphoreSlim(1, 1);

		private EnrollmentSessionManager()
		{
			_cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
		}

		public string StartEnrollmentSession(string fingerType, int qualityThreshold)
		{
			var sessionId = Guid.NewGuid().ToString();
			var session = new EnrollmentSession
			{
				SessionId = sessionId,
				StartTime = DateTime.UtcNow,
				FingerType = fingerType,
				QualityThreshold = qualityThreshold,
				Status = "waiting",
				Progress = 0,
				Instruction = "Place your finger on the scanner...",
				EnrollmentService = new FingerprintEnroll()
			};

			_sessions[sessionId] = session;
			Task.Run(() => StartEnrollmentProcess(session));
			return sessionId;
		}

		public EnrollmentSession GetSession(string sessionId)
		{
			_sessions.TryGetValue(sessionId, out var session);
			return session;
		}

		public void RemoveSession(string sessionId)
		{
			if (_sessions.TryRemove(sessionId, out var session))
			{
				try { session.EnrollmentService?.Dispose(); } catch { }
			}
		}

		private void StartEnrollmentProcess(EnrollmentSession session)
		{
			try
			{
				// Only one active capture session should access the device at a time
				_deviceLock.Wait();

				session.EnrollmentService.OnSampleProcessed += (progress, status, instruction) =>
				{
					session.Progress = progress;
					session.Status = status;
					session.Instruction = instruction;
				};

				// DPFP SDK works more reliably when capture runs on an STA thread
				bool success = false;
				byte[] templateBytes = null;
				Exception workerException = null;
				var worker = new System.Threading.Thread(() =>
				{
					try
					{
						success = session.EnrollmentService.TryEnroll(SESSION_TIMEOUT_SECONDS * 1000, out templateBytes);
					}
					catch (Exception ex)
					{
						workerException = ex;
					}
				})
				{ IsBackground = true };
				try { worker.SetApartmentState(System.Threading.ApartmentState.STA); } catch { }
				worker.Start();
				worker.Join();

				if (workerException != null) throw workerException;
				if (success && templateBytes != null)
				{
					session.Status = "completed";
					session.Progress = 100;
					session.Instruction = "Fingerprint captured successfully!";
					session.Template = Convert.ToBase64String(templateBytes);
					session.Quality = 85.0;
				}
				else
				{
					session.Status = "failed";
					session.Progress = 0;
					session.Instruction = "Scan failed. Please try again.";
					session.Error = "Timeout or low quality scan";
				}
			}
			catch (Exception ex)
			{
				session.Status = "failed";
				session.Progress = 0;
				session.Instruction = "Scan failed. Please try again.";
				session.Error = $"Device error: {ex.Message}";
			}
			finally
			{
				try { _deviceLock.Release(); } catch { }
				session.IsActive = false;
			}
		}

		private void CleanupExpiredSessions(object state)
		{
			var cutoffTime = DateTime.UtcNow.AddSeconds(-SESSION_TIMEOUT_SECONDS - 5);
			var expired = _sessions.Where(kvp => kvp.Value.StartTime < cutoffTime).ToList();
			foreach (var kvp in expired)
			{
				RemoveSession(kvp.Key);
			}
		}
	}
}
