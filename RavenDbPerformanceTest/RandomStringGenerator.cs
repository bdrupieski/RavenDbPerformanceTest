using System;
using System.Text;

namespace RavenDbPerformanceTest
{
    public class RandomStringGenerator
    {
        private static readonly Random Random = new Random();

        public static string RandomString(int size)
        {
            var builder = new StringBuilder();

            for (int i = 0; i < size; i++)
            {
                char ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * Random.NextDouble() + 65)));
                builder.Append(ch);
            }

            return builder.ToString();
        } 
    }
}