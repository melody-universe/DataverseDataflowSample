using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DotNetFramework
{
    static class ISourceBlockExtensions
    {
        public static Task ForEach<TType>(
            this ISourceBlock<TType> sourceBlock,
            Action<TType> action
        )
        {
            var actionBlock = new ActionBlock<TType>(action);
            sourceBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });
            return actionBlock.Completion;
        }

        public static Task ForEach<TType>(
            this ISourceBlock<TType> sourceBlock,
            Func<TType, Task> action,
            int boundedCapacity = OrganizationServiceTaskOrchestrator.DefaultMaxDegreesOfParallelism
        )
        {
            var actionBlock = new ActionBlock<TType>(
                action,
                new ExecutionDataflowBlockOptions { BoundedCapacity = boundedCapacity }
            );
            sourceBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });
            return actionBlock.Completion;
        }
    }
}
