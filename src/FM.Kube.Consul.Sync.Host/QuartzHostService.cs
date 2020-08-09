using FM.K8S.Consul.Sync.Service.Jobs;
using Microsoft.Extensions.Hosting;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FM.Kube.Consul.Sync.Host
{
    public class QuartzHostService : IHostedService
    {
        private readonly IScheduler _scheduler;

        public QuartzHostService(IScheduler scheduler)
        {
            _scheduler = scheduler;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            IJobDetail kubeToConsulJob = JobBuilder.Create<FromKubeToConsulJob>()
                .WithIdentity(nameof(FromKubeToConsulJob)).Build();
            ITrigger kubeToConsulJobTrigger = TriggerBuilder.Create().StartNow()
            .WithSimpleSchedule()
            .Build();
            await _scheduler.ScheduleJob(kubeToConsulJob, kubeToConsulJobTrigger);

            IJobDetail consulToKubeJob = JobBuilder.Create<FromConsulToKubeJob>()
              .WithIdentity(nameof(FromConsulToKubeJob)).Build();
            ITrigger consulToKubeTrigger = TriggerBuilder.Create().StartNow()
            .WithSimpleSchedule()
            .Build();
            await _scheduler.ScheduleJob(consulToKubeJob, consulToKubeTrigger);

            

        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
