using Assistant.Net.Dynamics.Abstractions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;

namespace Assistant.Net.Dynamics.Proxy.Runtime.Benchmarks
{
    public interface IBenchmarkTarget
    {
        string Method(string a);
    }

    public class BenchmarkTarget : IBenchmarkTarget
    {
        public string Method(string a) => a;
    }

    [MemoryDiagnoser]
    public class ProxyInvocationBenchmarks
    {
        private IBenchmarkTarget instance = null!;
        private IBenchmarkTarget uninterceptedProxy = null!;
        private IBenchmarkTarget interceptedProxy = null!;

        [GlobalSetup]
        public void Setup()
        {
            instance = new BenchmarkTarget();

            var factory = new ServiceCollection()
                .AddProxyFactory(o => o.Add<IBenchmarkTarget>())
                .AddDynamicProxyGeneration()
                .BuildServiceProvider()
                .GetRequiredService<IProxyFactory>();

            uninterceptedProxy = factory.Create<IBenchmarkTarget>(instance).Object;
            interceptedProxy = factory.Create<IBenchmarkTarget>(instance)
                .Intercept(x => x.Method(default!), (_, args) => (string) args[0]!)
                .Object;
        }

        [Benchmark(Baseline = true)]
        public string DirectCall() => instance.Method("1");

        [Benchmark]
        public string ProxyCall_fastPath() => uninterceptedProxy.Method("1");

        [Benchmark]
        public string ProxyCall_intercepted() => interceptedProxy.Method("1");
    }

    public static class Program
    {
        public static void Main(string[] args) => BenchmarkRunner.Run<ProxyInvocationBenchmarks>();
    }
}
