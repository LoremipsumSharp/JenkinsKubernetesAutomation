using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Model.Consul
{
    public class ConsulNotificationBase : INotification
    {
        public string ConsulServiceName
        {
            get; set;
        }

        public string KubeNamepsace { get; set; }
        public string FixConsulServiceNameForKube => ConsulServiceName.Replace(".", "-").Replace("_","-").ToLower();

    }
}
