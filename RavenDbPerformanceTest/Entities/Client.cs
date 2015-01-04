using System;

namespace RavenDbPerformanceTest.Entities
{
    public class Client : Entity
    {
        public Client(string firstName, string lastName, DateTime birthDate)
        {
            FirstName = firstName;
            LastName = lastName;
            BirthDate = birthDate;
        }

        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public DateTime BirthDate { get; private set; }
    }
}