using FM.K8S.Consul.Sync.Model.Consul;
using KubeClient;
using KubeClient.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Service.Handlers
{
    public class ConsulNotificationHandler :
          INotificationHandler<UpsertServiceEntryNotification>,
          INotificationHandler<RemoveServiceNotification>

    {
        private readonly IKubeApiClient _kubeApiClient;
        private readonly ILogger<ConsulNotificationHandler> _logger;

        public ConsulNotificationHandler(IKubeApiClient client,ILogger<ConsulNotificationHandler> logger)
        {
            _kubeApiClient = client;
            _logger = logger;
        }
        public async Task Handle(UpsertServiceEntryNotification notification, CancellationToken cancellationToken)
        {

            await AddOrUpdateKubeService(notification);
            await AddOrUpdateKubeEnpoints(notification);

        }

        private async Task AddOrUpdateKubeService(UpsertServiceEntryNotification notification)
        {
            try
            {
                if (notification.ServiceEntries.Any(x => x.Service.Tags.Contains("k8s"))) return;
                var serviceName = notification.FixConsulServiceNameForKube;
                var serviceClient = _kubeApiClient.ServicesV1();
                var service = await serviceClient.Get(serviceName, notification.KubeNamepsace);
                var kubeService = notification.ToKubeService();

                if (service == null)
                {

                    await serviceClient.Create(kubeService);
                }
                else
                {

                    if (!IsValidKubeService(kubeService)) return;
                    if (service.Metadata.Labels.TryGetValue("consul", out var result) && result == "true") return;
                    await serviceClient.Update(serviceName, patch =>
                    {
                        kubeService.Spec.ClusterIP = service.Spec.ClusterIP;
                        patch.Replace(
                            path: svc => svc.Spec,
                            value: kubeService.Spec
                            );
                    }, notification.KubeNamepsace);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"error occur:{DateTime.Now}");
            }

        }
        private async Task AddOrUpdateKubeEnpoints(UpsertServiceEntryNotification notification)
        {
            try
            {

                if (notification.ServiceEntries.Any(x => x.Service.Tags.Contains("k8s"))) return;
                var epName = notification.FixConsulServiceNameForKube;
                var epClient = _kubeApiClient.EndpointsV1();
                var epResult = await epClient.Get(epName, notification.KubeNamepsace);
                var kubeEP = notification.ToKubeEndpoints();

                if (epResult == null)
                {
                    await epClient.Create(kubeEP);
                }

                else
                {
                    if (epResult.Metadata.Labels.TryGetValue("consul", out var result) && result == "true") return;
                    await epClient.Update(epName, patch =>
                    {
                        patch.Replace(
                            path: endpoints => endpoints.Subsets,
                            value: kubeEP.Subsets
                            );
                    }, notification.KubeNamepsace);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"error occur:{DateTime.Now}");
            }
        }

        public async Task Handle(RemoveServiceNotification notification, CancellationToken cancellationToken)
        {
            var serviceClient = _kubeApiClient.ServicesV1();
            var service = await serviceClient.Get(notification.FixConsulServiceNameForKube, notification.KubeNamepsace);
            if (service != null && service.Metadata.Labels["consul"] == "true")
            {
                await serviceClient.Delete(notification.FixConsulServiceNameForKube, notification.KubeNamepsace);
            }
        }


        private bool IsValidKubeService(ServiceV1 kubeSvc) => kubeSvc.Spec.Ports.Any();
    }
}
