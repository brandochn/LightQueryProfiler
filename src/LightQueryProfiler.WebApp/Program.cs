/*
 Code adapted from:
https://github.com/chromelyapps/demo-projects/tree/master/blazor
September 2022
 */

using Chromely;
using Chromely.Core;
using Chromely.Core.Configuration;
using LightQueryProfiler.Shared.Util;
using LightQueryProfiler.WebApp;

var configFile = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false)
                .Build();

// Determine if the app will run as Web application or it will run as Desktop application 
bool runAsWebApp = Convert.ToBoolean(configFile["RunAsWebApp"]);

if (runAsWebApp)
{
    var builder = WebApplication.CreateBuilder(args);

    var startup = new Startup(builder.Configuration);
    startup.ConfigureServices(builder.Services);

    var app = builder.Build();
    startup.Configure(app, builder.Environment);
    app.Run();
}
else
{
    string tempPath = ServerAppUtil.CreateTempFile(Path.GetTempPath());
    bool firstProcess = ServerAppUtil.IsMainProcess(args);
    int port = ServerAppUtil.AvailablePort;

    if (firstProcess)
    {
        if (port != -1)
        {
            // start the kestrel server in a background thread
            AppDomain.CurrentDomain.ProcessExit += ServerAppUtil.ProcessExit;
            ServerAppUtil.BlazorTaskTokenSource = new CancellationTokenSource();
            ServerAppUtil.BlazorTask = new Task(() =>
            {
                BlazorAppBuilder.Create(args, port)
                    .Build()
                    .Run();
            }, ServerAppUtil.BlazorTaskTokenSource.Token, TaskCreationOptions.LongRunning);
            ServerAppUtil.BlazorTask.Start();

            // wait till its up
            while (ServerAppUtil.IsPortAvailable(port))
            {
                Thread.Sleep(1);
            }
        }

        // Save port for later use by chromely processes
        ServerAppUtil.SavePort(tempPath, port);
    }
    else
    {
        // fetch port number
        port = ServerAppUtil.GetSavedPort(tempPath);
    }

    if (port != -1)
    {
        // start up chromely
        var config = DefaultConfiguration.CreateForRuntimePlatform();
        config.WindowOptions.Title = "Light Query Profiler";
        config.StartUrl = $"https://127.0.0.1:{port}";
#if DEBUG
        // show/hide chrome dev tools
        config.DebuggingMode = true;
#else
    config.DebuggingMode = false;
#endif

        config.WindowOptions.WindowState = Chromely.Core.Host.WindowState.Maximize;
        //config.WindowOptions.RelativePathToIconFile = "chromely.ico";

        try
        {
            var builder = AppBuilder.Create(args);
            builder = builder.UseConfig<DefaultConfiguration>(config);
            builder = builder.UseApp<LightQueryProfilerApp>();
            builder = builder.Build();
            builder.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }
}

public class LightQueryProfilerApp : ChromelyBasicApp
{
    public override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
    }
}