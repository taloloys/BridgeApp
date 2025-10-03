using System.Web.Http;

namespace DeviceBridge.Controllers
{
	[RoutePrefix("api/health")]
	public class HealthController : ApiController
	{
		[HttpGet, Route("ping")]
		public IHttpActionResult Ping()
		{
			return Ok(new { status = "ok" });
		}
	}
}
