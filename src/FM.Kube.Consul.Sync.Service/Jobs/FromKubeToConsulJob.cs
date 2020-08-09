using FM.K8S.Consul.Sync.Model.Kube;
using FM.K8S.Consul.Sync.Model.Options;
using KubeClient;
using KubeClient.Models;
using KubeClient.ResourceClients;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Quartz;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Service.Jobs
{

    /// <summary>
    ///  后台调度服务，同步K8S服务到Consul
    /// </summary>
    public class FromKubeToConsulJob : IJob
    {
        private readonly IKubeApiClient _kubeClient;
        private readonly IMediator _mediator;
        private SyncOptions _syncOptions;
        private ILogger<FromKubeToConsulJob> _logger;
        private readonly ConcurrentDictionary<string, IDisposable> _namespaceWatching;

        public FromKubeToConsulJob(IKubeApiClient kubeClient,
            IMediator mediator, IOptions<SyncOptions> syncOptions, ILogger<FromKubeToConsulJob> logger)
        {
            _kubeClient = kubeClient;
            _mediator = mediator;
            _syncOptions = syncOptions.Value;
            _logger = logger;
            _namespaceWatching = new ConcurrentDictionary<string, IDisposable>();
        }

        public Task Execute(IJobExecutionContext context)
        {

            // 开始观察配置
            _kubeClient.ConfigMapsV1().Watch("sync-options", "default").Subscribe(
                  onNext: async notification =>
                  {
                      var newOptions = JsonConvert.DeserializeObject<SyncOptions>(notification.Resource.Data["sync-options"]);
                      var diff = _syncOptions.DiffKubeNsToSync(newOptions);
                      _syncOptions = newOptions;
                      foreach (var ns in diff.add)
                      {
                          await BenginWatchService(ns);
                      }

                  },
                  onError: error =>
                  {
                      //_logger.LogError(error, $"error occur durning handle the notification :{DateTime.Now}");
                  },
                  onCompleted: () =>
                  {
                  });
            return Task.CompletedTask;


        }

        private Task StopWatchService(string namespaceName)
        {
            if (_namespaceWatching.TryGetValue(namespaceName, out var subscription))
            {
                subscription.Dispose();
            }
            return Task.CompletedTask;
        }    
        private Task BenginWatchService(string namespaceName)
        {
            var kubeSvcClient = (ServiceClientV1)_kubeClient.ServicesV1();
            var observable = kubeSvcClient.WatchAll("consul!=true", namespaceName);
            var subscription = observable.Subscribe(
                onNext: async notification =>
                {
                    await Notify(notification);
                },
           onError: error =>
           {
               //   _logger.LogError(error, $"error occur durning handle the notification :{DateTime.Now}");
           },
           onCompleted: () =>
           {
           });
            _namespaceWatching.AddOrUpdate(namespaceName, subscription, (k, v) => subscription);
            return Task.CompletedTask;
        }


        private async Task Notify(IResourceEventV1<ServiceV1> notification)
        {
            switch (notification.EventType)
            {
                case ResourceEventType.Added:
                case ResourceEventType.Modified:
                    await _mediator.Publish(new UpSertServiceNotification()
                    {
                        KubeService = notification.Resource,
                        IngressPort = _syncOptions.IngressPort,
                        IngressTLSPort = _syncOptions.IngressTLSPort,
                    });
                    break;
                case ResourceEventType.Deleted:
                    await _mediator.Publish(new DeletedServiceNotification()
                    {
                        KubeService = notification.Resource,
                        IngressPort = _syncOptions.IngressPort,
                         IngressTLSPort = _syncOptions.IngressTLSPort,
                    });
                    break;
                case ResourceEventType.Error:
                    _logger.LogError($"error ResourceEventType : {JsonConvert.SerializeObject(notification)}");
                    break;
                default:
                    break;
            }
        }


    }
}
