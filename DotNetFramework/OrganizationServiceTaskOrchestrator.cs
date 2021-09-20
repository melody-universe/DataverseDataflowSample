using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DotNetFramework
{
    class OrganizationServiceTaskOrchestrator
    {
        public const int DefaultMaxDegreesOfParallelism = 52;

        private readonly CrmServiceClient crmServiceClient;
        private readonly int maxDegreesOfParallelism;

        static OrganizationServiceTaskOrchestrator()
        {
            // https://docs.microsoft.com/en-us/powerapps/developer/data-platform/api-limits#optimize-your-connection
            ServicePointManager.DefaultConnectionLimit = 65000;
            ThreadPool.SetMinThreads(100, 100);
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
        }

        public OrganizationServiceTaskOrchestrator(
            CrmServiceClient crmServiceClient,
            int maxDegreesOfParallelism = DefaultMaxDegreesOfParallelism
        )
        {
            this.crmServiceClient = crmServiceClient;
            this.maxDegreesOfParallelism = maxDegreesOfParallelism;
            _actionBlock = new Lazy<ActionBlock<Action<IOrganizationService>>>(CreateActionBlock);
        }

        private ActionBlock<Action<IOrganizationService>> CreateActionBlock() =>
            new ActionBlock<Action<IOrganizationService>>(
                action =>
                {
                    var service = crmServiceClient.Clone();
                    action(service);
                    service.Dispose();
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = maxDegreesOfParallelism
                }
            );
        private Lazy<ActionBlock<Action<IOrganizationService>>> _actionBlock;
        private ActionBlock<Action<IOrganizationService>> actionBlock
        {
            get => _actionBlock.Value;
        }

        public Task Run(Action<IOrganizationService> action) =>
            Run(
                service =>
                {
                    action(service);
                    return true;
                }
            );

        public Task<TResult> Run<TResult>(Func<IOrganizationService, TResult> function)
        {
            var deferred = new TaskCompletionSource<TResult>();
            actionBlock.Post(
                service =>
                {
                    var result = function(service);
                    deferred.SetResult(result);
                }
            );
            return deferred.Task;
        }
    }
}
