using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel;

namespace PinpointGeospatial.PinpointReports
{
    public class Startup
    {

        public static class PinpointConfiguration
        {
            public static IConfiguration Configuration;
        }

        public Startup(IConfiguration configuration)
        {
            TypeDescriptor.AddAttributes(typeof(System.Drawing.Image), new TypeConverterAttribute(typeof(TypeConverter)));
            Configuration = configuration;
            PinpointConfiguration.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            //services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("MzQ1MDI0QDMxMzgyZTMzMmUzMENuL0d1Rm1jaUJIL2dJR1FHdEphanJveWVJeEtiMHYzOXEzWFp6bTN0SlU9");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            //app.UseMvc();
        }
    }
}
