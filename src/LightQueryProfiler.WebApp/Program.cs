using ElectronNET.API;
using LightQueryProfiler.WebApp;

static IHostBuilder CreateHostBuilder(string[] args) =>
   Host.CreateDefaultBuilder(args)
   .ConfigureWebHostDefaults(webBuilder =>
   {
       webBuilder.UseElectron(args);
       webBuilder.UseEnvironment("Development");
       webBuilder.UseStartup<Startup>();
   });

CreateHostBuilder(args)
    .Build()
    .Run();
