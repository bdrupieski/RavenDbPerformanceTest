using System;
using System.Diagnostics;

namespace RavenDbPerformanceTest
{
    public class SimpleProfiler : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _stopwatch;

        public SimpleProfiler(string name)
        {
            _name = name;
            _stopwatch = Stopwatch.StartNew();
        }

        public static SimpleProfiler Start(string name)
        {
            Console.WriteLine(name + "...");
            return new SimpleProfiler(name);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            Console.WriteLine("{0} took {1} ms", _name, _stopwatch.ElapsedMilliseconds);
        }
    }
}