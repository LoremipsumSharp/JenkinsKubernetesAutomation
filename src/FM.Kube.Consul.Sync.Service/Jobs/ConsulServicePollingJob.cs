using Consul;
using FM.K8S.Consul.Sync.Model.Consul;
using FM.K8S.Consul.Sync.Model.Options;
using MediatR;
using Quartz;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Service.Jobs
{
    /// <summary>
    ///  监控单个服务的注册
    /// </summary>
    public class ConsulServicePollingJob : IJob
    {
        private readonly IConsulClient _consulClient;
        private readonly IMediator _mediator;
        public ConsulServicePollingJob(IConsulClient client, IMediator mediator)
        {
            _consulClient = client;
            _mediator = mediator;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            var svcNs = (ConsulSvcMapKubeNs)context.JobDetail.JobDataMap["svc_ns"];

            var queryOptions = new QueryOptions()
            {
                WaitTime = TimeSpan.FromMinutes(5),
                WaitIndex = 1
            };

            while (true)
            {

                if (!ConsulServicePollingRegistry.ConsulServiceEntryMap.TryGetValue(svcNs, out var existEntries))
                {
                 
                    break;
                }
                var queryResult = await _consulClient.Health.Service(
                service: svcNs.ConsulSvcName,
                tag: null,
                passingOnly: false,
                q: queryOptions);
                queryOptions.WaitIndex = queryResult.LastIndex;
                var entries = queryResult.Response?.ToList() ?? new List<ServiceEntry>();

                var hasAdd = entries
                    .Where(x => !existEntries.Any(y => y.Service.Address == x.Service.Address && y.Service.Port == x.Service.Port))
                    .Count() > 0;

                var hasRemove = existEntries
                    .Where(x => !entries.Any(y => y.Service.Address == x.Service.Address && y.Service.Port == x.Service.Port))
                    .Count() > 0;

                if (hasAdd || hasRemove)
                {
                    await _mediator.Publish(new UpsertServiceEntryNotification()
                    {
                        ServiceEntries = entries,
                        ConsulServiceName = svcNs.ConsulSvcName,
                        KubeNamepsace = svcNs.KubeNsName
                    });
                }
                ConsulServicePollingRegistry.ConsulServiceEntryMap.AddOrUpdate(svcNs, entries, (k, v) => entries);
            }
        }
    }
    public class ConsulServicePollingRegistry
    {
        public static readonly ConcurrentDictionary<ConsulSvcMapKubeNs, List<ServiceEntry>> ConsulServiceEntryMap = new ConcurrentDictionary<ConsulSvcMapKubeNs, List<ServiceEntry>>();
        private readonly IConsulClient _consulClient;
        private readonly IMediator _mediator;
        private readonly IScheduler _scheduler;
        public ConsulServicePollingRegistry(IConsulClient client, IMediator mediator, IScheduler scheduler)
        {

            _consulClient = client;
            _mediator = mediator;
            _scheduler = scheduler;
        }


        public async Task Regist(ConsulSvcMapKubeNs svcNs)
        {
            var serviceEntries = new List<ServiceEntry>();

            ConsulServiceEntryMap.AddOrUpdate(svcNs, serviceEntries, (k, v) => serviceEntries);

            await StartPolling(svcNs);

        }

        public async Task DeRegist(ConsulSvcMapKubeNs svcNs)
        {
            if (ConsulServiceEntryMap.TryRemove(svcNs, out var entry))
            {
                await CancelPoolling(svcNs);
            }
         
        }

        private async Task CancelPoolling(ConsulSvcMapKubeNs svcNs)
        {
            var jobIdentity = $"{nameof(ConsulServicePollingJob)}_{svcNs.KubeNsName}_{svcNs.ConsulSvcName}";
            var jobKey = new JobKey(jobIdentity);
            if (await _scheduler.CheckExists(new JobKey(jobIdentity)))
            {
                await _scheduler.PauseJob(jobKey);
                await _scheduler.DeleteJob(jobKey);
                await _mediator.Publish(new RemoveServiceNotification()
                {
                    ConsulServiceName = svcNs.ConsulSvcName,
                    KubeNamepsace = svcNs.KubeNsName,
                });
            }
        }

        private async Task StartPolling(ConsulSvcMapKubeNs svcNs)
        {

            var jobIdentity = $"{nameof(ConsulServicePollingJob)}_{svcNs.KubeNsName}_{svcNs.ConsulSvcName}";
            if (await _scheduler.CheckExists(new JobKey(jobIdentity))) return;
            IJobDetail job = JobBuilder.Create<ConsulServicePollingJob>().WithIdentity(jobIdentity).Build();
            job.JobDataMap["svc_ns"] = svcNs;
            ITrigger trigger = TriggerBuilder.Create().StartNow()
                .WithSimpleSchedule()
                .Build();
            await _scheduler.ScheduleJob(job, trigger);

        }
    }
}
