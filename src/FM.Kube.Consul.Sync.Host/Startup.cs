using System;
using System.IO;
using Consul;
using FM.K8S.Consul.Sync.Common.Quartz;
using FM.K8S.Consul.Sync.Model.Options;
using FM.K8S.Consul.Sync.Service.Handlers;
using FM.K8S.Consul.Sync.Service.Jobs;
using FM.Kube.Consul.Sync.Host;
using KubeClient;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FM.K8S.Consul.Sync.Host
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }
        public IHostingEnvironment Env { get; }
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
            Env = env;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(consulConfig =>
            {
                var address = Configuration["ConsulConfiguration:Address"];
                consulConfig.Address = new Uri(address);
            }));
            
            services.AddSingleton<IKubeApiClient>(sp =>
            {
                
                KubeClientOptions clientOptions =
                K8sConfig.Load(Path.Combine(Env.ContentRootPath, $"kube.config.{Env.EnvironmentName}.yaml")).ToKubeClientOptions(defaultKubeNamespace: "dotnet");
                return KubeApiClient.Create(clientOptions);
            });

            services.AddMediatR(cfg => cfg.AsSingleton(), typeof(ConsulNotificationHandler).Assembly);
            services.AddSingleton<ConsulServicePollingRegistry>();
            services.AddQuartz(typeof(ConsulServicePollingJob).Assembly);
            services.AddHostedService<QuartzHostService>();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Map("/healthcheck", _ => _.Run(async context => { await context.Response.WriteAsync("OK"); }));
        }
    }
}
