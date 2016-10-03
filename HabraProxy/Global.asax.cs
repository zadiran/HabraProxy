using HabraProxy.Controllers;
using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace HabraProxy
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            // Redirect all requests to proxy
            var routeData = new RouteData();
            routeData.Values["controller"] = "Proxy";
            routeData.Values["action"] = "Index";
            routeData.Values["path"] = Request.Url;
            IController controller = new ProxyController();
            var requestContext = new RequestContext(new HttpContextWrapper(Context), routeData);
            controller.Execute(requestContext);
            Response.End();
        }
    }
}