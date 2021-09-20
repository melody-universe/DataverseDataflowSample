using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DotNetFramework
{
    class DataverseQueryRunner
    {
        private readonly OrganizationServiceTaskOrchestrator taskRunner;
        private readonly int pageSize;

        public DataverseQueryRunner(
            OrganizationServiceTaskOrchestrator taskRunner,
            int pageSize = OrganizationServiceTaskOrchestrator.DefaultMaxDegreesOfParallelism
        )
        {
            this.taskRunner = taskRunner;
            this.pageSize = pageSize;
        }

        public ISourceBlock<Entity> CreateSourceBlock(QueryBase query)
        {
            var block = new BufferBlock<Entity>(
                new DataflowBlockOptions { BoundedCapacity = pageSize }
            );
            Task.Run(
                async () =>
                {
                    var pages = GetPages(query);
                    foreach (var page in pages)
                    {
                        foreach (var record in page)
                        {
                            await block.SendAsync(record);
                        }
                    }
                    block.Complete();
                }
            );
            return block;
        }

        private IEnumerable<IEnumerable<Entity>> GetPages(QueryBase query)
        {
            if (query is QueryExpression)
            {
                return ProcessQueryExpression((QueryExpression)query);
            }
            else if (query is FetchExpression)
            {
                return ProcessFetchExpression((FetchExpression)query);
            }
            else
            {
                throw new NotSupportedException($"Unsupported query type: {query.GetType()}");
            }
        }

        private IEnumerable<DataCollection<Entity>> ProcessQueryExpression(QueryExpression query)
        {
            if (query.PageInfo == null)
            {
                query.PageInfo = new PagingInfo();
            }
            query.PageInfo.Count = pageSize;
            query.PageInfo.PageNumber = 1;

            EntityCollection results;
            do
            {
                lock (Program.NumberOfContactsDeletedLock)
                {
                    Console.WriteLine(
                        $"Retrieving page {query.PageInfo.PageNumber} ({Program.NumberOfContactsDeleted} contacts deleted)"
                    );
                }
                results = taskRunner.Run(service => service.RetrieveMultiple(query))
                    .GetAwaiter()
                    .GetResult();
                Console.WriteLine($"Retrieved {results.Entities.Count} contacts.");
                yield return results.Entities;
                query.PageInfo.PagingCookie = results.PagingCookie;
                query.PageInfo.PageNumber++;
            } while (results.MoreRecords);
        }

        private IEnumerable<IEnumerable<Entity>> ProcessFetchExpression(FetchExpression queryBase)
        {
            throw new NotImplementedException();
        }
    }
}
