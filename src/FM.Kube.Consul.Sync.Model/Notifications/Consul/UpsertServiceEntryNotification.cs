using Consul;
using KubeClient.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Model.Consul
{
    public class UpsertServiceEntryNotification : ConsulNotificationBase
    {
        public List<ServiceEntry> ServiceEntries { get; set; }



        public ServiceV1 ToKubeService()
        {
            var svcPort = 5000;

            var service = new ServiceV1();
            service.ApiVersion = "v1";
            service.Kind = "Service";
            service.Metadata = new ObjectMetaV1();
            service.Metadata.Name = FixConsulServiceNameForKube;
            service.Metadata.Namespace = this.KubeNamepsace;
            service.Metadata.Labels.Add("consul", "true");
            service.Metadata.Labels.Add("consul_svc_name", ConsulServiceName);
            service.Metadata.Annotations.Add("consul.hashicorp.com/service-sync", "false");
            service.Spec = new ServiceSpecV1();
            ServiceEntries.ForEach(x =>
            {
                service.Spec.Ports.Add(new ServicePortV1()
                {
                    Protocol = "TCP",
                    Name = BuildPortName(x),
                    Port = svcPort,
                    TargetPort = x.Service.Port
                });
                svcPort++;
            });

            return service;
        }

        public EndpointsV1 ToKubeEndpoints()
        {
            var endpoints = new EndpointsV1();
            endpoints.ApiVersion = "v1";
            endpoints.Kind = "Endpoints";
            endpoints.Metadata = new ObjectMetaV1();
            endpoints.Metadata.Namespace = KubeNamepsace;
            endpoints.Metadata.Name = FixConsulServiceNameForKube;
            endpoints.Metadata.Labels.Add("consul", "true");
            ServiceEntries.ForEach(x =>
            {

                if (!IPAddress.TryParse(x.Service.Address, out var result))
                {
                    return;
                }
                var subset = new EndpointSubsetV1();
               
                subset.Addresses.Add(new EndpointAddressV1()
                {
                    Ip = x.Service.Address
                });
                subset.Ports.Add(new EndpointPortV1()
                {
                    Name = BuildPortName(x),
                    Port = x.Service.Port,
                    Protocol = "TCP"
                });

                endpoints.Subsets.Add(subset);

            });

            return endpoints;
        }

      
        private string BuildPortName(ServiceEntry entry) => $"{entry.Node.Name}-{FixConsulServiceNameForKube}";
    }
}
