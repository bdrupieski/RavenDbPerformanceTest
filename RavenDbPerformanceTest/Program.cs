using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Raven.Abstractions.Data;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Linq;
using RavenDbPerformanceTest.Entities;
using RavenDbPerformanceTest.Indexes;

namespace RavenDbPerformanceTest
{
    class Program
    {
        public static int IndexCount = 100;
        public static int DocumentCount = 4000;

        static void Main(string[] args)
        {
            var dir = new DirectoryInfo("Data");
            if (dir.Exists)
            {
                dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;
                dir.Delete(true);
            }

            var documentStore = new EmbeddableDocumentStore
            {
                DataDirectory = "Data",
                UseEmbeddedHttpServer = true,
            };

            using (SimpleProfiler.Start("Initializing DocumentStore"))
            {
                documentStore.Initialize();
            }

            //using (SimpleProfiler.Start("Deleting all indexes"))
            //{
            //    DeleteAllIndexes(documentStore);
            //}

            //using (SimpleProfiler.Start("Deleting all documents"))
            //{
            //    DeleteAllDocuments<Client>(documentStore);
            //    DeleteAllDocuments<Application>(documentStore);
            //}

            using (SimpleProfiler.Start("Benchmark"))
            {
                using (SimpleProfiler.Start("Creating indexes"))
                {
                    CreateIndexes(documentStore, IndexCount);
                }

                using (SimpleProfiler.Start("Creating documents"))
                {
                    CreateDocuments(documentStore, DocumentCount);
                }

                using (SimpleProfiler.Start("Waiting for stale indexes"))
                {
                    WaitForStaleIndexes(documentStore);
                }

                using (SimpleProfiler.Start("Querying for documents"))
                {
                    QueryForDocuments(documentStore, IndexCount);
                }
            }

            while (true)
            {
                Console.WriteLine("sleeping");
                Thread.Sleep(30000);
            }
        }

        public static void CreateIndexes(IDocumentStore documentStore, int count)
        {
            var def = new ClientApplication().CreateIndexDefinition();

            for (int i = 1; i <= count; i++)
            {
                var name = string.Format("{0}{1}", typeof(ClientApplication).Name, i);
                documentStore.DatabaseCommands.PutIndex(name, def);
            }
        }

        public static void DeleteAllDocuments<T>(IDocumentStore documentStore)
        {
            using (IDocumentSession session = documentStore.OpenSession())
            {
                string tagName = session.Advanced.DocumentStore.Conventions.GetTypeTagName(typeof(T));

                session.Advanced.DocumentStore.DatabaseCommands.DeleteByIndex("Raven/DocumentsByEntityName",
                    new IndexQuery { Query = "Tag:" + tagName }, true).WaitForCompletion();
            }
        }

        public static void DeleteAllIndexes(IDocumentStore documentStore)
        {
            using (IDocumentSession session = documentStore.OpenSession())
            {
                var indexNames = session.Advanced.DocumentStore.DatabaseCommands.GetIndexNames(0, 500);

                foreach (var indexName in indexNames.Where(x => !x.Contains("Raven")))
                {
                    session.Advanced.DocumentStore.DatabaseCommands.DeleteIndex(indexName);
                }
            }
        }

        public static void CreateDocuments(IDocumentStore documentStore, int count)
        {
            var firstNames = File.ReadAllLines(Path.Combine("Names", "FirstNames.txt"));
            var lastNames = File.ReadAllLines(Path.Combine("Names", "LastNames.txt"));
            var r = new Random();

            for (int i = 0; i < count; i++)
            {
                var session = documentStore.OpenSession();

                string firstName = firstNames[r.Next(firstNames.Length)];
                string lastName = firstNames[r.Next(lastNames.Length)];

                var client = new Client(firstName, lastName, DateTime.Now - TimeSpan.FromDays(r.Next(30, 20000)));
                session.Store(client);
                session.SaveChanges();

                var app = new Application(client.Id, DateTime.Now - TimeSpan.FromDays(r.Next(2, 100)), RandomStringGenerator.RandomString(340));
                session.Store(app);
                session.SaveChanges();
            }
        }

        public static void WaitForStaleIndexes(IDocumentStore documentStore)
        {
            while (documentStore.DatabaseCommands.GetStatistics().StaleIndexes.Length != 0)
            {
                Thread.Sleep(500);
            }
        }

        public static void QueryForDocuments(IDocumentStore documentStore, int indexCount)
        {
            var firstFortyLastNames = File.ReadAllLines(Path.Combine("Names", "LastNames.txt")).Take(40).ToArray();

            for (int i = 1; i <= indexCount; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();

                var indexName = string.Format("{0}{1}", typeof(ClientApplication).Name, i);
                var docs = documentStore.OpenSession().Query<ClientApplication.IndexResult>(indexName)
                    .Where(x => x.BirthDate < new DateTime(1990, 1, 1) ||
                                x.Data.StartsWith("A") ||
                                x.FirstName == "Bernardo" ||
                                x.LastName.In(firstFortyLastNames))
                    .Take(10000)
                    .ToList();

                sw.Stop();
                Console.WriteLine("{0} documents queried from {1} in {2} ms", docs.Count, indexName, sw.ElapsedMilliseconds);
            }
        }
    }
}
