using System.Web.Http;
using DeviceBridge.Services;

namespace DeviceBridge.Controllers
{
	[RoutePrefix("api/devices")]
	public class DevicesController : ApiController
	{
		[HttpGet, Route("")]
		public IHttpActionResult Get()
		{
			if (FingerprintService.TryDetectReader(out var model, out var error))
				return Ok(new
				{
					success = true,
					status = "ok",
					available = true,
					model = model,
					device = new { present = true, model = model }
				});

			return Ok(new
			{
				success = false,
				status = "error",
				available = false,
				model = (string)null,
				device = new { present = false, model = (string)null },
				error
			});
		}

		
	}
}

