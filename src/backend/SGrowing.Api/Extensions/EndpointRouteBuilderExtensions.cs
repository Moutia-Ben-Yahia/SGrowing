using System.Reflection;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Extensions;

/// <summary>
/// Provides extension methods on <see cref="IEndpointRouteBuilder"/> for modular endpoint discovery and auto-registration.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Scans the given assemblies (or the executing assembly if none are provided) for concrete classes implementing
    /// <see cref="IEndpointModule"/> and invokes <see cref="IEndpointModule.MapEndpoints"/> on an instance of each.
    /// </summary>
    /// <param name="app">The endpoint route builder to register endpoints onto.</param>
    /// <param name="assemblies">The assemblies to scan for endpoint modules. Defaults to the executing assembly if not specified.</param>
    /// <returns>The original <paramref name="app"/> instance for fluent method chaining.</returns>
    public static IEndpointRouteBuilder MapEndpointModules(this IEndpointRouteBuilder app, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(app);

        var targetAssemblies = assemblies is { Length: > 0 }
            ? assemblies
            : [Assembly.GetExecutingAssembly()];

        var moduleTypes = targetAssemblies
            .Distinct()
            .SelectMany(GetLoadableTypes)
            .Where(t => typeof(IEndpointModule).IsAssignableFrom(t)
                        && t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                        && t.GetConstructor(Type.EmptyTypes) is not null);

        foreach (var type in moduleTypes)
        {
            if (Activator.CreateInstance(type) is IEndpointModule module)
            {
                module.MapEndpoints(app);
            }
        }

        return app;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
