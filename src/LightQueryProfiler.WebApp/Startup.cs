using ElectronNET.API;
using ElectronNET.API.Entities;
using LightQueryProfiler.SharedWebUI.Data;
using LightQueryProfiler.WebApp.Data;
using Blazored.Modal;

namespace LightQueryProfiler.WebApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Add services to the container.
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddSingleton<WeatherForecastService>();
            services.AddScoped<LightQueryProfilerInterop>();
            services.AddBlazoredModal();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // For regular
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });

            if (HybridSupport.IsElectronActive)
            {
                ElectronCreateWindow();
            }
        }

        public async void ElectronCreateWindow()
        {
            var browserWindowOptions = new BrowserWindowOptions
            {
                Show = false, // wait to open it
                WebPreferences = new WebPreferences
                {
                    WebSecurity = false
                }
            };

            var browserWindow = await Electron.WindowManager.CreateWindowAsync(browserWindowOptions);
            await browserWindow.WebContents.Session.ClearCacheAsync();

            // Handler to show when it is ready
            browserWindow.OnReadyToShow += () =>
            {
                browserWindow.Show();
            };

            // Close Handler
            browserWindow.OnClose += () => Environment.Exit(0);
            browserWindow.SetTitle("Light Query Profiler");
            browserWindow.Maximize();
        }
    }
}