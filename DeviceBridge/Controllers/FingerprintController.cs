using System;
using System.Web.Http;
using DeviceBridge.Services;
using DeviceBridge.Models;

namespace DeviceBridge.Controllers
{
	[RoutePrefix("api/fingerprint")]
	public class FingerprintController : ApiController
	{
		[HttpPost, Route("enroll")]
		public IHttpActionResult Enroll()
		{
			using (var enroller = new FingerprintEnroll())
			{
				var ok = enroller.TryEnroll(timeoutMs: 30_000, out var tpl);
				if (!ok || tpl == null) return BadRequest("Enrollment timed out or failed.");
				var b64 = Convert.ToBase64String(tpl);
				return Ok(new { template = b64, format = "DPFP", quality = (int?)null });
			}
		}

		[HttpPost, Route("enroll/start")]
		public IHttpActionResult StartEnrollment([FromBody] StartEnrollmentRequest request)
		{
			try
			{
				if (request == null) request = new StartEnrollmentRequest();
				if (!string.IsNullOrEmpty(request.finger_type) && request.finger_type != "index" && request.finger_type != "thumb")
					return BadRequest("finger_type must be 'index' or 'thumb'");
				if (request.quality_threshold < 1 || request.quality_threshold > 100)
					return BadRequest("quality_threshold must be between 1 and 100");

				var sessionId = EnrollmentSessionManager.Instance.StartEnrollmentSession(
					request.finger_type ?? "index",
					request.quality_threshold);

				return Ok(new StartEnrollmentResponse
				{
					success = true,
					session_id = sessionId,
					status = "waiting",
					message = "Place finger on scanner"
				});
			}
			catch (Exception ex)
			{
				return InternalServerError(ex);
			}
		}

		[HttpGet, Route("enroll/progress/{sessionId}")]
		public IHttpActionResult GetEnrollmentProgress(string sessionId)
		{
			if (string.IsNullOrEmpty(sessionId)) return BadRequest("Session ID is required");
			var session = EnrollmentSessionManager.Instance.GetSession(sessionId);
			if (session == null) return NotFound();

			var response = new EnrollmentProgressResponse
			{
				session_id = session.SessionId,
				progress = session.Progress,
				status = session.Status,
				instruction = session.Instruction,
				template = session.Template,
				quality = session.Quality,
				error = session.Error
			};

			if (session.Status == "completed" || session.Status == "failed")
			{
				System.Threading.Tasks.Task.Delay(5000).ContinueWith(_ =>
					EnrollmentSessionManager.Instance.RemoveSession(sessionId));
			}

			return Ok(response);
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
	}
}


