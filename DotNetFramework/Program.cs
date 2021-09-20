using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace DotNetFramework
{
    static class Program
    {
        static void Main(string[] _)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Dataverse"].ConnectionString;
            var service = new CrmServiceClient(connectionString);
            var taskOrchestrator = new OrganizationServiceTaskOrchestrator(service);
            var queryRunner = new DataverseQueryRunner(taskOrchestrator);

            WriteTimeSpan("Initialized runners");
            CreateContacts(taskOrchestrator);
            WriteTimeSpan("Created contacts");
            WriteContacts(queryRunner);
            WriteTimeSpan("Wrote contacts");
            DeleteContacts(taskOrchestrator, queryRunner);
            WriteTimeSpan("Deleted contacts");
        }

        static DateTime lastTime = DateTime.Now;
        static void WriteTimeSpan(string message)
        {
            var newTime = DateTime.Now;
            Console.WriteLine($"{newTime - lastTime}: {message}");
            lastTime = newTime;
        }

        static void CreateContacts(OrganizationServiceTaskOrchestrator runner, int count = 1000)
        {
            var tasks = new List<Task>();
            for (var i = 0; i < count; i++)
            {
                var contact = new Entity("contact");
                contact["firstname"] = i.ToString();
                var task = runner.Run(
                    service =>
                    {
                        service.Create(contact);
                    }
                );
                tasks.Add(task);
            }
            Task.WaitAll(tasks.ToArray());
        }

        static void WriteContacts(DataverseQueryRunner queryRunner)
        {
            var contactsBlock = RetrieveContacts(queryRunner);
            contactsBlock.ForEach(
                    contact =>
                        Console.WriteLine(
                            $"Retrieved contact {contact.GetAttributeValue<string>("firstname")}"
                        )
                )
                .GetAwaiter()
                .GetResult();
        }

        public static object NumberOfContactsDeletedLock = new object();
        public static int NumberOfContactsDeleted = 0;
        static void DeleteContacts(
            OrganizationServiceTaskOrchestrator taskOrchestrator,
            DataverseQueryRunner queryRunner
        )
        {
            var contactsBlock = RetrieveContacts(queryRunner);
            contactsBlock.ForEach(
                    contact =>
                        taskOrchestrator.Run(
                            service =>
                            {
                                service.Delete(contact.LogicalName, contact.Id);
                                lock (NumberOfContactsDeletedLock)
                                {
                                    NumberOfContactsDeleted++;
                                }
                            }
                        )
                )
                .GetAwaiter()
                .GetResult();
        }

        static ISourceBlock<Entity> RetrieveContacts(DataverseQueryRunner queryRunner)
        {
            var query = new QueryExpression("contact") { ColumnSet = new ColumnSet("firstname") };
            return queryRunner.CreateSourceBlock(query);
        }
    }
}
