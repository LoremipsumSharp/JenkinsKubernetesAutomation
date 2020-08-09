using HTTPlease;
using KubeClient.Models;
using Microsoft.AspNetCore.JsonPatch;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubeClient.ResourceClients
{

    /// <summary>
    /// 
    /// </summary>
    public class EndpointsClientV1: KubeResourceClient, IEndpointsClientV1
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public EndpointsClientV1(IKubeApiClient client)
            : base(client)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newEnpoints"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<EndpointsV1> Create(EndpointsV1 newEnpoints, CancellationToken cancellationToken = default)
        {
            if (newEnpoints == null)
                throw new ArgumentNullException(nameof(newEnpoints));

            return await Http
                .PostAsJsonAsync(
                    Requests.Collection.WithTemplateParameters(new
                    {
                        Namespace = newEnpoints?.Metadata?.Namespace ?? KubeClient.DefaultNamespace
                    }),
                    postBody: newEnpoints,
                    cancellationToken: cancellationToken
                )
                .ReadContentAsObjectV1Async<EndpointsV1>(
                    operationDescription: $"create v1/Endpoints resource in namespace '{newEnpoints?.Metadata?.Namespace ?? KubeClient.DefaultNamespace}'"
                );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="kubeNamespace"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<StatusV1> Delete(string name, string kubeNamespace = null, CancellationToken cancellationToken = default)
        {
            return await Http
              .DeleteAsync(
                  Requests.ByName.WithTemplateParameters(new
                  {
                      Name = name,
                      Namespace = kubeNamespace ?? KubeClient.DefaultNamespace
                  }),
                  cancellationToken: cancellationToken
              )
              .ReadContentAsObjectV1Async<StatusV1>(
                  $"delete v1/Service resource '{name}' in namespace '{kubeNamespace ?? KubeClient.DefaultNamespace}'",
                  HttpStatusCode.OK, HttpStatusCode.NotFound
              );
        }
        
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="kubeNamespace"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<EndpointsV1> Get(string name, string kubeNamespace = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'name'.", nameof(name));

            return await GetSingleResource<EndpointsV1>(
                Requests.ByName.WithTemplateParameters(new
                {
                    Name = name,
                    Namespace = kubeNamespace ?? KubeClient.DefaultNamespace
                }),
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="labelSelector"></param>
        /// <param name="kubeNamespace"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<EndpointsListV1> List(string labelSelector = null, string kubeNamespace = null, CancellationToken cancellationToken = default)
        {
            return await GetResourceList<EndpointsListV1>(
                Requests.Collection.WithTemplateParameters(new
                {
                    Namespace = kubeNamespace ?? KubeClient.DefaultNamespace,
                    LabelSelector = labelSelector
                }),
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="patchAction"></param>
        /// <param name="kubeNamespace"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<EndpointsV1> Update(string name, Action<JsonPatchDocument<EndpointsV1>> patchAction, string kubeNamespace = null, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'name'.", nameof(name));

            if (patchAction == null)
                throw new ArgumentNullException(nameof(patchAction));

            return await PatchResource(patchAction,
                Requests.ByName.WithTemplateParameters(new
                {
                    Name = name,
                    Namespace = kubeNamespace ?? KubeClient.DefaultNamespace
                }),
                cancellationToken
            );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="kubeNamespace"></param>
        /// <returns></returns>
        public IObservable<IResourceEventV1<EndpointsV1>> Watch(string name, string kubeNamespace = null)
        {
            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Argument cannot be null, empty, or entirely composed of whitespace: 'name'.", nameof(name));

            return ObserveEvents<EndpointsV1>(
                Requests.WatchByName.WithTemplateParameters(new
                {
                    Name = name,
                    Namespace = kubeNamespace ?? KubeClient.DefaultNamespace
                }),
                operationDescription: $"watch v1/Endpoints '{name}' in namespace {kubeNamespace ?? KubeClient.DefaultNamespace}"
            );
        }
        /// <summary>
        ///     Request templates for the Service (v1) API.
        /// </summary>
        static class Requests
        {
            /// <summary>
            ///     A collection-level Service (v1) request.
            /// </summary>
            public static readonly HttpRequest Collection = KubeRequest.Create("api/v1/namespaces/{Namespace}/endpoints?labelSelector={LabelSelector?}&watch={Watch?}");

            /// <summary>
            ///     A get-by-name Service (v1) request.
            /// </summary>
            public static readonly HttpRequest ByName = KubeRequest.Create("api/v1/namespaces/{Namespace}/endpoints/{Name}");

            /// <summary>
            ///     A watch-by-name Service (v1) request.
            /// </summary>
            public static readonly HttpRequest WatchByName = KubeRequest.Create("api/v1/watch/namespaces/{Namespace}/endpoints/{Name}");
        }
    }


    /// <summary>
    /// 
    /// </summary>
    public interface IEndpointsClientV1
       : IKubeResourceClient
    {
       /// <summary>
       /// 
       /// </summary>
       /// <param name="name"></param>
       /// <param name="kubeNamespace"></param>
       /// <param name="cancellationToken"></param>
       /// <returns></returns>
        Task<EndpointsV1> Get(string name, string kubeNamespace = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="labelSelector"></param>
        /// <param name="kubeNamespace"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<EndpointsListV1> List(string labelSelector = null, string kubeNamespace = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="kubeNamespace"></param>
        /// <returns></returns>
        IObservable<IResourceEventV1<EndpointsV1>> Watch(string name, string kubeNamespace = null);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="newEnpoints"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<EndpointsV1> Create(EndpointsV1 newEnpoints, CancellationToken cancellationToken = default);

     
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="patchAction"></param>
        /// <param name="kubeNamespace"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<EndpointsV1> Update(string name, Action<JsonPatchDocument<EndpointsV1>> patchAction, string kubeNamespace = null, CancellationToken cancellationToken = default);



        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="kubeNamespace"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<StatusV1> Delete(string name, string kubeNamespace = null, CancellationToken cancellationToken = default);
    }
}
