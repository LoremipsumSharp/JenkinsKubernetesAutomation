using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Model.Options
{
    /// <summary>
    ///  保存在k8s为k8s的configmap
    /// </summary>
    public class SyncOptions
    {
        public int IngressPort { get; set; }
        public int IngressTLSPort { get; set; }

        public HashSet<string> KubeNamespacesToSync { get; set; } = new HashSet<string>();

        // 需要同步到k8s的consul服务，因为k8s有命名空间，所以需要这个配置项，不然不知道consul服务最后是同步到
        // 哪个k8s的命名空间
        public ConcurrentDictionary<string, HashSet<string>> KubeNsMapConsulSvc { get; set; }
        = new ConcurrentDictionary<string, HashSet<string>>();


        public (HashSet<string> add,HashSet<string> delete) DiffKubeNsToSync(SyncOptions diff)
        {
            var add = new HashSet<string>(diff.KubeNamespacesToSync.Except(this.KubeNamespacesToSync));
            var delete = new HashSet<string>(this.KubeNamespacesToSync.Except(diff.KubeNamespacesToSync));

            return (add, delete);

        }

        public (Dictionary<string, HashSet<string>> add, Dictionary<string, HashSet<string>> delete) DiffNsConsulMap(SyncOptions toDiff)
        {

            var add = new Dictionary<string, HashSet<string>>();

            var delete = new Dictionary<string, HashSet<string>>();

            if (toDiff == null) return (add, delete);

            // 新增的命名空间配置
            foreach (var ns in toDiff.KubeNsMapConsulSvc.Keys)
            {
                if (!this.KubeNsMapConsulSvc.Keys.Any(x => x == ns))
                {
                    add.Add(ns, toDiff.KubeNsMapConsulSvc[ns]);
                }
                else
                {
                    foreach (var consulServiceName in toDiff.KubeNsMapConsulSvc[ns])
                    {
                        if (!this.KubeNsMapConsulSvc[ns].Contains(consulServiceName))
                        {
                            if (!add.ContainsKey(ns)) add.Add(ns, new HashSet<string>());
                            add[ns].Add(consulServiceName);
                        }
                    }
                }
            }

            foreach (var ns in this.KubeNsMapConsulSvc.Keys)
            {
                if (!toDiff.KubeNsMapConsulSvc.Keys.Any(x => x == ns))
                {
                    delete.Add(ns, this.KubeNsMapConsulSvc[ns]);
                }
                else
                {
                    foreach (var consulServiceName in this.KubeNsMapConsulSvc[ns])
                    {
                        if (!toDiff.KubeNsMapConsulSvc[ns].Contains(consulServiceName))
                        {
                            if (!delete.ContainsKey(ns)) delete.Add(ns, new HashSet<string>());
                            delete[ns].Add(consulServiceName);
                        }
                    }
                }

            }

            return (add, delete);
        }


    }


    public class ConsulSvcMapKubeNs
    {
        public string ConsulSvcName { get; set; }
        public string KubeNsName { get; set; }

        public override bool Equals(object obj)
        {
            var objToCompare = (ConsulSvcMapKubeNs)obj;
            return this.ConsulSvcName == objToCompare.ConsulSvcName
                && this.KubeNsName == objToCompare.KubeNsName;
        }
        public override int GetHashCode()
        {
            return ConsulSvcName.GetHashCode();
        }
    }
}
