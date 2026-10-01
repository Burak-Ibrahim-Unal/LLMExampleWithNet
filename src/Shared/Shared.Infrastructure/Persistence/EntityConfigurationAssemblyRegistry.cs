using System.Reflection;

namespace Shared.Infrastructure.Persistence;

public sealed class EntityConfigurationAssemblyRegistry
{
    public EntityConfigurationAssemblyRegistry(IEnumerable<Assembly> assemblies)
    {
        Assemblies = assemblies.Distinct().ToArray();
    }

    public IReadOnlyCollection<Assembly> Assemblies { get; }
}
