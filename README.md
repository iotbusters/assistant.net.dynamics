# Assistant.NET Dynamics

This is a subset of the [Assistant.NET](https://github.com/iotbusters/assistant.net/blob/master/README.md) solution.
The solution is planned to help with dynamic code generation like proxy or mapping.

Currently, it's in design and implementation stage, so the repository contains mostly tools and infrastructure parts only.
Existing releases cannot be assumed as stable and backward compatible too, so pay attention during package upgrade!

Hopefully, it will be useful for someone once main functional is ready.

Please join this [quick survey](https://forms.gle/eB3sN5Mw76WMpT6w5).

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## Packages

A family of standalone packages serve Assistant.NET Dynamics needs and being [freely](license) distributed
at [nuget.org](https://nuget.org). Each of them has own responsibility and solves some specific aspect of the solution.

### assistant.net.dynamics.*

It's reserved for code usage analysis, runtime and compile-forward optimizations. E.g. Proxies, mappings etc.

#### assistant.net.dynamics.proxy.builder

Proxy source code generation tool.

#### assistant.net.dynamics.proxy.runtime

Proxy generation tool that supports precompiled proxies and runtime proxy generation depending on chosen strategy.

```csharp
services.AddProxyFactory(o => o
    .AllowConfiguredOnlyGeneration() // default strategy
    .Add<Interface>()));

var factory = provider.GetRequiredService<IProxyFactory>();
var proxy = factory.Create<Interface>()
    .Intercept(x => x.Method(), (next, methodInfo, args) => "result")
    .Object;
var result = proxy.Method(); // "result"
```

#### assistant.net.dynamics.proxy.analyzer

Analysis based proxy generation tool that supports compile-forward proxy generation according to the usage `factory.Create<Interface>()`.

\*.csproj file

```xml
  <ItemGroup>
    <PackageReference Include="assistant.net.dynamics.proxy.analyzer" Version="0.0.11">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="assistant.net.dynamics.proxy.runtime" Version="0.0.11" />
  </ItemGroup>
```
