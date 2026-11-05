using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;
using SGrowing.Api.Extensions;
using SGrowing.Api.Modules.Dev.Gateway;
using Xunit;

namespace SGrowing.Tests.Common;

public class EndpointModuleDiscoveryTests
{
    private sealed class DummyRouteBuilder : IEndpointRouteBuilder
    {
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
        public IServiceProvider ServiceProvider { get; }

        public DummyRouteBuilder(IServiceProvider? serviceProvider = null)
        {
            var services = new ServiceCollection();
            services.AddDevGatewayServices();
            ServiceProvider = serviceProvider ?? services.BuildServiceProvider();
        }

        public IApplicationBuilder CreateApplicationBuilder()
        {
            throw new NotImplementedException();
        }
    }

    private class TestEndpointModule : IEndpointModule
    {
        public static bool WasMapped { get; set; }

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            WasMapped = true;
        }
    }

    private abstract class AbstractEndpointModule : IEndpointModule
    {
        public void MapEndpoints(IEndpointRouteBuilder app)
        {
        }
    }

    [Fact]
    public void DevGatewayEndpoints_Implements_IEndpointModule()
    {
        var module = new DevGatewayEndpoints();
        module.Should().BeAssignableTo<IEndpointModule>();
    }

    [Fact]
    public void MapEndpointModules_ThrowsWhenAppIsNull()
    {
        Action act = () => ((IEndpointRouteBuilder)null!).MapEndpointModules();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapEndpointModules_DiscoversAndMaps_EndpointModulesInSpecifiedAssembly()
    {
        TestEndpointModule.WasMapped = false;
        var builder = new DummyRouteBuilder();

        builder.MapEndpointModules(typeof(EndpointModuleDiscoveryTests).Assembly);

        TestEndpointModule.WasMapped.Should().BeTrue();
    }

    [Fact]
    public void MapEndpointModules_DiscoversDevGatewayEndpoints_InApiAssembly()
    {
        var builder = new DummyRouteBuilder();

        // Should scan Api assembly without error
        var result = builder.MapEndpointModules(typeof(DevGatewayEndpoints).Assembly);

        result.Should().BeSameAs(builder);
    }
}
