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
    /// Scans the given assemblies (or the executing assembly if none are provided) for:
    /// 1. Concrete classes implementing <see cref="IEndpointModule"/> and invokes <see cref="IEndpointModule.MapEndpoints"/> on an instance of each.
    /// 2. Public static endpoint mapping methods in <c>SGrowing.Api.Modules.*</c> (named <c>Map*Endpoints</c> accepting <see cref="IEndpointRouteBuilder"/>).
    /// Tracks mapped types and methods in a deduplication set to ensure no module or method is ever mapped twice.
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

        var mappedIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allTypes = targetAssemblies
            .Distinct()
            .SelectMany(GetLoadableTypes)
            .ToList();

        // 1. Discover and instantiate IEndpointModule implementations
        var moduleTypes = allTypes
            .Where(t => typeof(IEndpointModule).IsAssignableFrom(t)
                        && t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                        && t.GetConstructor(Type.EmptyTypes) is not null)
            .ToList();

        foreach (var type in moduleTypes)
        {
            var typeKey = type.FullName ?? type.Name;
            if (mappedIdentifiers.Add(typeKey))
            {
                mappedIdentifiers.Add(type.Name);
                mappedIdentifiers.Add($"Map{type.Name}");

                if (type.Namespace is not null)
                {
                    mappedIdentifiers.Add($"{type.Namespace}.{type.Name}");
                    mappedIdentifiers.Add($"{type.Namespace}.Map{type.Name}");

                    // Suppress companion static methods in the same namespace (e.g. CrmEndpointExtensions.MapCrmEndpoints)
                    var relatedMethods = allTypes
                        .Where(t => t.Namespace == type.Namespace)
                        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        .Where(m => m.Name.StartsWith("Map", StringComparison.Ordinal) && m.Name.EndsWith("Endpoints", StringComparison.Ordinal));

                    foreach (var rm in relatedMethods)
                    {
                        if (rm.DeclaringType?.FullName is not null)
                        {
                            mappedIdentifiers.Add($"{rm.DeclaringType.FullName}.{rm.Name}");
                        }
                        mappedIdentifiers.Add($"{type.Namespace}.{rm.Name}");
                    }
                }

                if (Activator.CreateInstance(type) is IEndpointModule module)
                {
                    module.MapEndpoints(app);
                }
            }
        }

        // 2. Discover public static endpoint mapping methods in SGrowing.Api.Modules.*
        var staticMappingMethods = allTypes
            .Where(t => t.Namespace is not null && (t.Namespace.StartsWith("SGrowing.Api.Modules", StringComparison.Ordinal) || t.Namespace.Contains(".Modules")))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.Name.StartsWith("Map", StringComparison.Ordinal)
                        && m.Name.EndsWith("Endpoints", StringComparison.Ordinal)
                        && (m.ReturnType == typeof(void) || typeof(IEndpointRouteBuilder).IsAssignableFrom(m.ReturnType)))
            .Where(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length == 1 && typeof(IEndpointRouteBuilder).IsAssignableFrom(parameters[0].ParameterType);
            })
            .ToList();

        foreach (var method in staticMappingMethods)
        {
            var declaringType = method.DeclaringType;
            var methodKey = $"{declaringType?.FullName}.{method.Name}";
            var namespaceKey = $"{declaringType?.Namespace}.{method.Name}";

            if (mappedIdentifiers.Contains(methodKey) || mappedIdentifiers.Contains(namespaceKey))
            {
                continue;
            }

            mappedIdentifiers.Add(methodKey);
            mappedIdentifiers.Add(namespaceKey);

            method.Invoke(null, [app]);
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
