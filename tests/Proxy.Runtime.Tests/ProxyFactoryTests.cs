using Assistant.Net.Dynamics.Abstractions;
using Assistant.Net.Dynamics.Proxy.Runtime.Tests.Mocks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Assistant.Net.Dynamics.Proxy.Runtime.Tests
{
    public class Tests
    {
        [Test]
        public void Proxy_interceptsGetOperations()
        {
            var factory = new ServiceCollection()
                .AddProxyFactory(o => o.Add<ITest>())
                .AddDynamicProxyGeneration()
                .BuildServiceProvider()
                .GetRequiredService<IProxyFactory>();

            var _ = KnownProxy.ProxyTypes;

            var proxy = factory.Create<ITest>()
                .Intercept(x => x.Method(), (_, _, _) => Task.CompletedTask)
                .Intercept(x => x.Method(""), (_, _, _) => "3")
                .Intercept(x => x.Property, "5")
                .Intercept(x => x.Function(), _ => "6")
                .Intercept(x => x.Function(default!), (_, _) => "7")
                .Intercept(x => x.Function(default!, default!), (_, _, _) => 8)
                .Object;

            proxy.ToString().Should().Be(proxy.GetType().FullName);
            proxy.Method().Should().Be(Task.CompletedTask);
            proxy.Method("1").Should().Be("3");
            proxy.Property.Should().Be("5");
            proxy.Function().Should().Be("6");
            proxy.Function(default!).Should().Be("7");
            proxy.Function(default!, default).Should().Be(8);
        }

        [Test]
        public void Proxy_throws_noBackedObjectAndNoInterceptors()
        {
            var factory = new ServiceCollection()
                .AddProxyFactory(o => o.Add<ITest>())
                .AddDynamicProxyGeneration()
                .BuildServiceProvider()
                .GetRequiredService<IProxyFactory>();

            var proxy = factory.Create<ITest>().Object;

            proxy.Invoking(x => { x.Method(); }).Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void Proxy_interceptsSetterOperations()
        {
            var factory = new ServiceCollection()
                .AddProxyFactory(o => o.Add<ITest>())
                .AddDynamicProxyGeneration()
                .BuildServiceProvider()
                .GetRequiredService<IProxyFactory>();

            string? captured = null;
            var proxy = factory.Create<ITest>()
                .InterceptSet<ITest, string>(x => x.Property3, v => captured = v)
                .Object;

            proxy.Property3 = "new-value";

            captured.Should().Be("new-value");
        }

        [Test]
        public void Proxy_forwardsOutParameter_whenNotIntercepted()
        {
            var factory = new ServiceCollection()
                .AddProxyFactory(o => o.Add<ITest>())
                .AddDynamicProxyGeneration()
                .BuildServiceProvider()
                .GetRequiredService<IProxyFactory>();

            var proxy = factory.Create<ITest>(new Test()).Object;

            var found = proxy.TryGet("a", out var result);

            found.Should().BeTrue();
            result.Should().Be("a!");
        }

        [Test]
        public void Proxy_writesBackOutParameter_whenIntercepted()
        {
            var factory = new ServiceCollection()
                .AddProxyFactory(o => o.Add<ITest>())
                .AddDynamicProxyGeneration()
                .BuildServiceProvider()
                .GetRequiredService<IProxyFactory>();

            var proxy = factory.Create<ITest>(new Test());
            var method = typeof(ITest).GetMethod(nameof(ITest.TryGet))!;
            ((IProxy) proxy).AddInterceptor(method, (_, args) =>
            {
                args[1] = "intercepted!";
                return true;
            });

            var found = proxy.Object.TryGet("a", out var result);

            found.Should().BeTrue();
            result.Should().Be("intercepted!");
        }

        [Test]
        public void Proxy_handlesConcurrentCreateAndInterceptorRegistration()
        {
            var factory = new ServiceCollection()
                .AddProxyFactory(o => o.Add<ITest>())
                .AddDynamicProxyGeneration()
                .BuildServiceProvider()
                .GetRequiredService<IProxyFactory>();

            var actions = Enumerable.Range(0, 50).Select(i => (Action) (() =>
            {
                var proxy = factory.Create<ITest>(new Test())
                    .Intercept(x => x.Function(), _ => $"value-{i}")
                    .Object;
                proxy.Function().Should().Be($"value-{i}");
            })).ToArray();

            Parallel.Invoke(actions);
        }
    }
}
