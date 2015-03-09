using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NDesk.Options;
using Raven.Abstractions.Data;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Linq;
using Raven.Database.Server;
using RavenDbPerformanceTest.Entities;
using RavenDbPerformanceTest.Indexes;
using StackExchange.Profiling;

namespace RavenDbPerformanceTest
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            const string s = @"
                                                 ,::::.._
                                               ,':::::::::.
   RavenDB Performance Tester Tool         _,-'`:::,::(o)::`-,.._
                                        _.', ', `:::::::::;'-..__`.
                                   _.-'' ' ,' ,' ,\:::,'::-`'''
                               _.-'' , ' , ,'  ' ,' `:::/
                         _..-'' , ' , ' ,' , ,' ',' '/::
                 _...:::'`-..'_, ' , ,'  , ' ,'' , ,'::|
              _`.:::::,':::::,'::`-:..'_',_'_,'..-'::,'|
      _..-:::'::,':::::::,':::,':,'::,':::,'::::::,':::;
        `':,'::::::,:,':::::::::::::::::':::,'::_:::,'/
        __..:'::,':::::::--''' `-:,':,':::'::-' ,':::/
   _.::::::,:::.-''-`-`..'_,'. ,',  , ' , ,'  ', `','
 ,::SSt:''''`                 \:. . ,' '  ,',' '_,'
                               ``::._,'_'_,',.-'
                                   \\ \\
                                    \\_\\
                                     \\`-`.-'_
                                  .`-.\\__`. ``
                                     ``-.-._
                                         `
";

            Console.WriteLine(s);

            int indexCount = 100;
            int documentCount = 4000;

            var options = new OptionSet
            {
                { "i|indexes=", "number of indexes to create", v => indexCount = int.Parse(v) },
                { "d|documents=", "number of documents*2 to create", v => documentCount = int.Parse(v) },
            };

            if (args.Length == 0)
            {
                Console.WriteLine("Usage:");
                options.WriteOptionDescriptions(Console.Out);
                Console.WriteLine("Using defaults of {0} indexes and {1} documents", indexCount, documentCount);
                Console.WriteLine();
            }
            else
            {
                options.Parse(args);
            }
            
            Console.WriteLine("Running with {0} {1} and {2} {3}... please wait.", 
                indexCount, indexCount == 1 ? "index" : "indexes",
                documentCount, documentCount == 1 ? "document" : "documents");

            var dir = new DirectoryInfo("Data");
            if (dir.Exists)
            {
                dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;
                dir.Delete(true);
            }

            MiniProfiler.Settings.ProfilerProvider = new SingletonProfilerProvider();
            MiniProfiler.Start("RavenDB Benchmark");

            var documentStore = new EmbeddableDocumentStore
            {
                DataDirectory = "Data",
                UseEmbeddedHttpServer = true,
            };

            NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(8080);

            using (MiniProfiler.Current.Step("Initializing DocumentStore"))
            {
                documentStore.Initialize();
            }

            //using (MiniProfiler.Current.Step("Deleting all indexes"))
            //{
            //    DeleteAllIndexes(documentStore);
            //}

            //using (MiniProfiler.Current.Step("Deleting all documents"))
            //{
            //    DeleteAllDocuments<Client>(documentStore);
            //    DeleteAllDocuments<Application>(documentStore);
            //}

            using (MiniProfiler.Current.Step("Create indexes"))
            {
                CreateIndexes(documentStore, indexCount);
            }

            using (MiniProfiler.Current.Step("Creating documents"))
            {
                CreateDocuments(documentStore, documentCount);
            }

            using (MiniProfiler.Current.Step("Waiting for stale indexes"))
            {
                WaitForStaleIndexes(documentStore);
            }

            using (MiniProfiler.Current.Step("Querying for documents"))
            {
                QueryForDocuments(documentStore, indexCount);
            }

            MiniProfiler.Stop();

            var report =
                Environment.CommandLine + Environment.NewLine +
                MiniProfiler.Current.RenderPlainText() + Environment.NewLine + Environment.NewLine;
            Console.WriteLine(report);
            
            File.AppendAllText("output.txt", report);
            Console.WriteLine("Results saved in output.txt.");
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine("Sleeping. You can open localhost:8080.");
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
            var r = new Random(42);

            for (int i = 0; i < count; i++)
            {
                var session = documentStore.OpenSession();

                string firstName = firstNames[r.Next(firstNames.Length)];
                string lastName = firstNames[r.Next(lastNames.Length)];

                var clientBirthday = DateTime.Now - TimeSpan.FromDays(r.Next(30, 20000));
                var client = new Client(firstName, lastName, clientBirthday);
                session.Store(client);
                session.SaveChanges();

                var applicationDate = DateTime.Now - TimeSpan.FromDays(r.Next(2, 100));
                var app = new Application(client.Id, applicationDate, RandomStringGenerator.RandomString(340));
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
            var first40LastNames = File.ReadAllLines(Path.Combine("Names", "LastNames.txt")).Take(40).ToArray();
            var first200FirstNames = File.ReadAllLines(Path.Combine("Names", "FirstNames.txt")).Take(200).ToArray();

            for (int i = 1; i <= indexCount; i++)
            {
                var indexName = string.Format("{0}{1}", typeof(ClientApplication).Name, i);

                var docs = documentStore.OpenSession().Query<ClientApplication.IndexResult>(indexName)
                    .Where(x => x.BirthDate < new DateTime(1990, 1, 1) ||
                                x.Data.StartsWith("A") ||
                                x.FirstName == "Bernardo" ||
                                x.LastName.In(first40LastNames))
                    .Take(10000)
                    .ToList();

                Debug.WriteLine("{0} documents", docs.Count);

                var docs2 = documentStore.OpenSession().Query<ClientApplication.IndexResult>(indexName)
                    .Where(x => (x.BirthDate < new DateTime(2000, 1, 1) && x.BirthDate > new DateTime(1990, 1, 1)) ||
                                x.FirstName.In(first200FirstNames) &&
                                x.LastName != null)
                    .Take(10000)
                    .ToList();

                Debug.WriteLine("{0} documents", docs2.Count);
            }
        }
    }
}