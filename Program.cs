using Serilog;
using Serilog.Events;
using Swagger.Net.Application;
using System;
using System.ServiceProcess;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.SelfHost;

namespace AyodiaSmartCard
{
    class Program
    {
        static void Main(string[] args)
        {
            //Log.Logger = new LoggerConfiguration()
            //    .MinimumLevel.Debug()
            //    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            //    .Enrich.FromLogContext()
            //    .WriteTo.File(@"C:\Windows\Temp\Ayodia-Smart-CardID\LogFile.txt")
            //    .CreateLogger();
            //var config = new HttpSelfHostConfiguration("http://127.0.0.1:8033");

            //config.Routes.MapHttpRoute(
            //    name: "API",
            //    routeTemplate: "api/{controller}/{action}/{id}",
            //    defaults: new { id = RouteParameter.Optional }
            //    );
            //config.MapHttpAttributeRoutes();
            //config.EnableCors(new EnableCorsAttribute("*", headers: "*", methods: "*"));
            //config.EnableSwagger(c => c.SingleApiVersion("1.0", "A title for your API")).EnableSwaggerUi();
            //using (HttpSelfHostServer server = new HttpSelfHostServer(config))
            //{

            //    server.OpenAsync().Wait();
            //    Console.WriteLine("Web API is started now.");
            //    Log.Information("Starting up the service");
            //    Console.ReadLine();
            //}

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new AyodiaSmartCardService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
