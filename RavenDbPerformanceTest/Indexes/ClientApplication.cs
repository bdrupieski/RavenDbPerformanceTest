using System;
using System.Linq;
using Raven.Client.Indexes;
using RavenDbPerformanceTest.Entities;

namespace RavenDbPerformanceTest.Indexes
{
    public class ClientApplication : AbstractMultiMapIndexCreationTask<ClientApplication.IndexResult>
    {
        public class IndexResult
        {
            public string ClientId { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public DateTime BirthDate { get; set; }
            public string ApplicationId { get; set; }
            public DateTime ApplicationDate { get; set; }
            public string Data { get; set; }
        }

        public ClientApplication()
        {
            AddMap<Client>(clients => from client in clients
                                      select new
                                      {
                                          ClientId = client.Id,
                                          FirstName = client.FirstName,
                                          LastName = client.LastName,
                                          BirthDate = client.BirthDate,
                                          ApplicationId = (string)null,
                                          ApplicationDate = (string)null,
                                          Data = (string)null,
                                      });

            AddMap<Application>(applications => from application in applications
                                                select new
                                                {
                                                    ClientId = application.ClientId,
                                                    FirstName = (string)null,
                                                    LastName = (string)null,
                                                    BirthDate = (string)null,
                                                    ApplicationId = application.Id,
                                                    ApplicationDate = application.ApplicationDate,
                                                    Data = application.Data,
                                                });

            Reduce = results => from result in results
                                group result by result.ClientId
                                into g
                                select new
                                {
                                    ClientId = g.Key,
                                    FirstName = g.Select(x => x.FirstName).FirstOrDefault(x => x != null),
                                    LastName = g.Select(x => x.LastName).FirstOrDefault(x => x != null),
                                    BirthDate = g.Select(x => x.BirthDate).FirstOrDefault(x => x != null),
                                    ApplicationId = g.Select(x => x.ApplicationId).FirstOrDefault(x => x != null),
                                    ApplicationDate = g.Select(x => x.ApplicationDate).FirstOrDefault(x => x != null),
                                    Data = g.Select(x => x.Data).FirstOrDefault(x => x != null),
                                };
        }
    }
}