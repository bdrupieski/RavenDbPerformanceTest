using System;

namespace RavenDbPerformanceTest.Entities
{
    public class Application : Entity
    {
        public Application(string clientId, DateTime applicationDate, string data)
        {
            ClientId = clientId;
            ApplicationDate = applicationDate;
            Data = data;
        }

        public string ClientId { get; private set; }
        public DateTime ApplicationDate { get; private set; }
        public string Data { get; private set; }
    }
}