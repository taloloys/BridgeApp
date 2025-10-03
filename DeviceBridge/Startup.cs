using System.Web.Http;
using Owin;
using System.Net.Http.Formatting;
using Microsoft.Owin.Cors;

namespace DeviceBridge
{
	public class Startup
	{
		public void Configuration(IAppBuilder app)
		{
			app.UseCors(CorsOptions.AllowAll);
			var config = new HttpConfiguration();
			config.Formatters.Clear();
			config.Formatters.Add(new JsonMediaTypeFormatter());
			config.MapHttpAttributeRoutes();
			config.Routes.MapHttpRoute("DefaultApi", "api/{controller}/{action}");
			app.UseWebApi(config);
		}
	}
}

