using Consul;
using FM.K8S.Consul.Sync.Model.Kube;
using FM.K8S.Consul.Sync.Model.Options;
using KubeClient;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Service.Handlers
{
    public class KubeNotificationHandler :
        INotificationHandler<UpSertServiceNotification>,
        INotificationHandler<DeletedServiceNotification>
    {
        private readonly IKubeApiClient _kubeClient;
        private readonly IConsulClient _consulClient;
        private readonly ILogger<KubeNotificationHandler> _logger;

        public KubeNotificationHandler(IKubeApiClient kubeClient, IConsulClient consuleClient, ILogger<KubeNotificationHandler> logger)
        {
            _kubeClient = kubeClient;
            _consulClient = consuleClient;
            _logger = logger;
        }


        public async Task Handle(DeletedServiceNotification notification, CancellationToken cancellationToken)
        {
            var ingressKubeClient = _kubeClient.IngressesV1Beta1();
            var labels = notification.KubeService.Metadata.Labels;
            // 如果这个服务是从consul同步过来的,不做任何处理
            if (labels.ContainsKey("consul") && labels["consul"] == "true") return;

            var ingress = notification.ToGrpcIngress();

            if (await ingressKubeClient.Get(ingress.Metadata.Name, ingress.Metadata.Namespace) != null)
            {
                await ingressKubeClient.Delete(ingress.Metadata.Name, ingress.Metadata.Namespace);
                await _consulClient.Agent.ServiceDeregister(notification.ToConsulRegistration().ID);
            }
        }

        public async Task Handle(UpSertServiceNotification notification, CancellationToken cancellationToken)
        {
            // 如果这个服务是从consul同步过来的,不做任何处理
            var labels = notification.KubeService.Metadata.Labels;
            if (labels.ContainsKey("consul") && labels["consul"] == "true") return;
            await AddOrUpdateIngress(notification);
            await AddOrUpdateConsulService(notification);

        }


        private async Task AddOrUpdateIngress(UpSertServiceNotification notification)
        {
            var ingressKubeClient = _kubeClient.IngressesV1Beta1();
            

            var ingress = notification.ToGrpcIngress();

            if (await ingressKubeClient.Get(ingress.Metadata.Name, ingress.Metadata.Namespace) == null)
            {
                await ingressKubeClient.Create(ingress);
            }
            else
            {
                await ingressKubeClient.Update(ingress.Metadata.Name, patch =>
                {
                    patch.Replace(
                        path: ing => ing.Spec,
                        value: ingress.Spec
                        );
                }, ingress.Metadata.Namespace);
            }
        }

        private async Task AddOrUpdateConsulService(UpSertServiceNotification notification)
        {
            var queryResult = await _consulClient.Health.Service(
              service: notification.ConsulServiceName,
              tag: "k8s",
              passingOnly: false
              );
            if(!queryResult.Response.ToList().Any())
            {
                await _consulClient.Agent.ServiceRegister(notification.ToConsulRegistration());
            }
        }
    }
}

