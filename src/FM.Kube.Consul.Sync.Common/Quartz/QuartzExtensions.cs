using FM.K8S.Consul.Sync.Common.Quartz.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Common.Quartz
{
    public static class QuartzExtensions
    {

        public static void AddQuartz(this IServiceCollection services, Assembly jobASM)
        {
            var jobTypes = jobASM.GetTypes().Where(x => typeof(IJob).IsAssignableFrom(x)).ToList();
            services.AddSingleton<IJobFactory, BatchJobFactory>();
            services.Add(jobTypes.Select(jobType => new ServiceDescriptor(jobType, jobType, ServiceLifetime.Transient)));

            services.AddSingleton(provider =>
            {

                var props = new NameValueCollection();
                props["quartz.serializer.type"] = "json";
                props["quartz.threadPool.threadCount"] = "1024";
                var schedulerFactory = new StdSchedulerFactory(props);
                var scheduler = schedulerFactory.GetScheduler().Result;
                scheduler.JobFactory = provider.GetService<IJobFactory>();
                scheduler.Clear();
                scheduler.Start();

                return scheduler;
            });

        }

        public static void ScheduleJob<TJob>(this IScheduler scheduler, ITrigger trigger)
            where TJob : IJob
        {
            var jobName = typeof(TJob).Name;

            var job = JobBuilder.Create<TJob>()
              .WithIdentity(jobName)
              .Build();

            scheduler.ScheduleJob(job, trigger);
        }

        public static void StartJob<TJob>(this IScheduler scheduler, ITrigger trigger)
  where TJob : IJob
        {

            var jobName = typeof(TJob).Name;

            var job = JobBuilder.Create<TJob>()
              .WithIdentity(jobName)
              .Build();

            scheduler.ScheduleJob(job, trigger);
        }
    }
}
