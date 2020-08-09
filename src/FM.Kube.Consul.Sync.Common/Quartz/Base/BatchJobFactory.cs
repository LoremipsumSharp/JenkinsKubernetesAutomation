using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FM.K8S.Consul.Sync.Common.Quartz.Base
{
    public class BatchJobFactory : IJobFactory
    {
        private readonly IServiceProvider _serviceProvider;
        public BatchJobFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {

            var scope = _serviceProvider.CreateScope();
            var jobDetail = bundle.JobDetail;
            var actualJob = (IJob)scope.ServiceProvider.GetService(jobDetail.JobType);
            var loggerFactory = (ILoggerFactory)scope.ServiceProvider.GetService(typeof(ILoggerFactory));
            return new ScopedJob(actualJob, scope, loggerFactory);

        }
        public void ReturnJob(IJob job)
        {

        }


        public class ScopedJob : IJob
        {

            private IJob actualJob;
            private IServiceScope scope;
            private ILogger logger;
            public ScopedJob(IJob actualJob, IServiceScope scope, ILoggerFactory facotry)
            {
                this.actualJob = actualJob;
                this.scope = scope;
                this.logger = facotry.CreateLogger(actualJob.GetType());
            }


            public async Task Execute(IJobExecutionContext context)
            {
                try
                {
                    await actualJob.Execute(context);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"failed to execute the job,time:{DateTime.Now}");
                }
                finally
                {
                    scope.Dispose();
                }

            }
        }
    }
}
