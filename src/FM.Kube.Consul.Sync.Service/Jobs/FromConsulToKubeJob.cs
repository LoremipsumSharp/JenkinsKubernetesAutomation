using Consul;
using FM.K8S.Consul.Sync.Model.Options;
using KubeClient;
using MediatR;
using Newtonsoft.Json;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Service.Jobs
{
    /// <summary>
    ///  后台调度线程：同步Consul服务到K8S
    /// </summary>
    public class FromConsulToKubeJob : IJob
    {
        private readonly IConsulClient _consulClient;
        private readonly QueryOptions _servicesQueryOptions;
        private Dictionary<string, string[]> _consuleService;
        private readonly IMediator _mediator;
        private readonly ConsulServicePollingRegistry _ps;
        private readonly IKubeApiClient _kubeClient;
        private SyncOptions _syncOptions;
        public FromConsulToKubeJob(IConsulClient consulClient, IMediator mediator, ConsulServicePollingRegistry monitor, IKubeApiClient kubeClient)
        {
            _consulClient = consulClient;
            _servicesQueryOptions = new QueryOptions()
            {
                WaitTime = TimeSpan.FromMinutes(5),
                WaitIndex = 1
            };
            _consuleService = new Dictionary<string, string[]>();
            _mediator = mediator;
            _ps = monitor;
            _kubeClient = kubeClient;
            _syncOptions = new SyncOptions();
        }
        public async Task Execute(IJobExecutionContext context)
        {

            await BeginWatchSyncOptions();
            await BeginWatchConsul();

        }

        /// <summary>
        ///  监控同步配置，只同步那些有被配置到consul服务
        /// </summary>
        /// <returns></returns>

        private Task BeginWatchSyncOptions()
        {
            // 开始观察配置
            _kubeClient.ConfigMapsV1().Watch("sync-options", "default").Subscribe(
                  onNext: async notification =>
                  {
                      var newOptions = JsonConvert.DeserializeObject<SyncOptions>(notification.Resource.Data["sync-options"]);
                      var diff = _syncOptions.DiffNsConsulMap(newOptions);
                      var addMappings = diff.add.SelectMany(x => x.Value.Select(y => new ConsulSvcMapKubeNs()
                      {
                          ConsulSvcName = y,
                          KubeNsName = x.Key

                      })).ToList();
                      var delMappings = diff.delete.SelectMany(x => x.Value.Select(y => new ConsulSvcMapKubeNs()
                      {
                          ConsulSvcName = y,
                          KubeNsName = x.Key
                      })).ToList();
                      await PollingService(addMappings, delMappings);
                      _syncOptions = newOptions;
                  },
                  onError: error =>
                  {
                  },
                  onCompleted: () =>
                  {
                  });
            return Task.CompletedTask;
        }


        private async Task BeginWatchConsul()
        {
            // 开始观察consul注册中心的
            while (true)
            {

                if (_syncOptions.KubeNsMapConsulSvc.IsEmpty)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                var queryResult = await _consulClient.Catalog.Services(_servicesQueryOptions);
                var services = queryResult.Response;

                // 较上次新增的服务（不包括那些k8s同步过来的服务，防止出现循环同步）
                var serviceToAdd = services.Keys.Where(x => !_consuleService.ContainsKey(x) && !services[x].Any(y => y == "k8s"))
                    .Intersect(_syncOptions.KubeNsMapConsulSvc.SelectMany(y => y.Value))
                    .Select(x => new ConsulSvcMapKubeNs()
                    {
                        ConsulSvcName = x,
                        KubeNsName = _syncOptions.KubeNsMapConsulSvc.Where(y => y.Value.Contains(x)).FirstOrDefault().Key

                    }).ToList();


                // 较上次减少的服务（不包括那些k8s同步过来的服务，防止出现循环同步）
                var serviceToRemove = _consuleService.Where(x => !services.Keys.Contains(x.Key) && !x.Value.Any(y => y == "k8s"))
                    .Select(x => x.Key).Intersect(_syncOptions.KubeNsMapConsulSvc.SelectMany(y => y.Value))
                    .Select(x => new ConsulSvcMapKubeNs()
                    {
                        ConsulSvcName = x,
                        KubeNsName = _syncOptions.KubeNsMapConsulSvc.Where(y => y.Value.Contains(x)).FirstOrDefault().Key
                    }).ToList();
                _consuleService = services;
                _servicesQueryOptions.WaitIndex = queryResult.LastIndex;
                await PollingService(serviceToAdd, serviceToRemove);

            }
        }



        private async Task PollingService(List<ConsulSvcMapKubeNs> serviceToAdd, List<ConsulSvcMapKubeNs> serviceToRemove)
        {
            foreach (var svc in serviceToAdd)
            {
                await _ps.Regist(svc);
            }
            foreach (var svc in serviceToRemove)
            {
                await _ps.DeRegist(svc);
            }
        }


    }
}
