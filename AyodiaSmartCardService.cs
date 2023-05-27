using Serilog.Events;
using Serilog;
using Swagger.Net.Application;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Cors;
using System.Web.Http;
using System.Web.Http.SelfHost;

namespace AyodiaSmartCard
{
    partial class AyodiaSmartCardService : ServiceBase
    {
        HttpSelfHostServer _server;
        public AyodiaSmartCardService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // TODO: Add code here to start your service.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(@"C:\Windows\Temp\Ayodia-Smart-CardID\LogFile.txt")
                .CreateLogger();
            var config = new HttpSelfHostConfiguration("http://127.0.0.1:8033");

            config.Routes.MapHttpRoute(
                name: "API",
                routeTemplate: "api/{controller}/{action}/{id}",
                defaults: new { id = RouteParameter.Optional }
                );
            config.MapHttpAttributeRoutes();
            config.EnableCors(new EnableCorsAttribute("*", headers: "*", methods: "*"));
            config.EnableSwagger(c => c.SingleApiVersion("1.0", "A title for your API")).EnableSwaggerUi();
            _server = new HttpSelfHostServer(config);


            Log.Information("Starting up the service");
            _server.OpenAsync().Wait();
        }



        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down necessary to stop your service.
            _server.CloseAsync().Wait();
        }
    }
}
