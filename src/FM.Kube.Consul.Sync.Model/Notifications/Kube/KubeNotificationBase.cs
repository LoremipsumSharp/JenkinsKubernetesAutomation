using Consul;
using KubeClient.Models;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Model.Kube
{
    public class KubeNotificationBase : INotification
    {
        public ServiceV1 KubeService { get; set; }

        public int IngressPort { get; set; }
        public int IngressTLSPort { get; set; }


        public IngressV1Beta1 ToGrpcIngress()
        {
            var ingressResouce = new IngressV1Beta1();
            ingressResouce.Kind = "Ingress";
            ingressResouce.ApiVersion = "extensions/v1beta1";
            ingressResouce.Metadata = new ObjectMetaV1();
            ingressResouce.Metadata.Name = $"{KubeService.Metadata.Name}-ingress";
            ingressResouce.Metadata.Namespace = KubeService.Metadata.Namespace;
            ingressResouce.Metadata.Labels.Add("traffic-type", "external");
            ingressResouce.Metadata.Annotations.Add("ingress.kubernetes.io/protocol", "h2c");
            ingressResouce.Metadata.Annotations.Add("kubernetes.io/ingress.class", "traefik");
            ingressResouce.Spec = new IngressSpecV1Beta1();

            var rule = new IngressRuleV1Beta1();
            rule.Host = IngressHostName;
            rule.Http = new HTTPIngressRuleValueV1Beta1();

            KubeService.Spec.Ports.ForEach(p =>
            {
                rule.Http.Paths.Add(new HTTPIngressPathV1Beta1()
                {
                    Backend = new IngressBackendV1Beta1()
                    {
                        ServiceName = KubeService.Metadata.Name,
                        ServicePort = p.Port
                    }
                });
            });
            ingressResouce.Spec.Rules.Add(rule);

            return ingressResouce;

        }

        public string ConsulServiceName
        {
            get
            {
                return KubeService.Metadata.Labels.ContainsKey("consul_svc_name") ?
                    KubeService.Metadata.Labels["consul_svc_name"] : KubeService.Metadata.Name;
            }
        }

        private string IngressHostName => $"{KubeService.Metadata.Name}.followme-internal.ingress";
        private string ConsulServiceId
        {
            get
            {
                var first = KubeService.Metadata.Labels.ContainsKey("consul_svc_name") ? KubeService.Metadata.Labels["consul_svc_name"] : KubeService.Metadata.Name;
                return first + "-" + "k8s" + "-" + "ingress";
            }
        }


        public AgentServiceRegistration ToConsulRegistration()
        {
            var registration = new AgentServiceRegistration()
            {
                ID = ConsulServiceId,
                Address = IngressHostName,
                Port = IngressPort,
                Name = KubeService.Metadata.Labels.ContainsKey("consul_svc_name") ? KubeService.Metadata.Labels["consul_svc_name"] : KubeService.Metadata.Name,
                Tags = new string[] { "k8s" },
                Check = new AgentCheckRegistration()
                {
                    TCP = $"{IngressHostName}:{IngressPort}",
                    Status = HealthStatus.Passing,
                    Timeout = TimeSpan.FromSeconds(3),
                    Interval = TimeSpan.FromSeconds(10),
                    DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(30),
                }
            };
            return registration;
        }
    }
}
