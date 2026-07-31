using System.Reflection;
using System.Runtime.Loader;

namespace Assistant.Net.Dynamics.Internal;

/// <summary>
///     Collectible load context for runtime-emitted proxy assemblies.
///     Unloading is best-effort: registered proxy factories keep the assembly alive until no longer referenced.
/// </summary>
internal sealed class ProxyLoadContext : AssemblyLoadContext
{
    /// <summary />
    public ProxyLoadContext() : base(name: "assistant.net.dynamics.proxy.dynamic", isCollectible: true) { }

    protected override Assembly? Load(AssemblyName assemblyName) => null;
}
