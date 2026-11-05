using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;
using SGrowing.Api.Extensions;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Auth;
using SGrowing.Api.Modules.Billing;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.Dev;
using SGrowing.Api.Modules.Dev.Gateway;
using SGrowing.Api.Modules.Diagnostics;
using SGrowing.Api.Modules.Funnels;
using SGrowing.Api.Modules.Journeys;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Modules.SEO;
using SGrowing.Api.Modules.Voice;
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

    [Theory]
    [InlineData(typeof(HealthEndpoints))]
    [InlineData(typeof(AuthEndpoints))]
    [InlineData(typeof(CrmEndpoints))]
    [InlineData(typeof(ConversationsEndpoints))]
    [InlineData(typeof(OutboundSdrEndpoints))]
    [InlineData(typeof(JourneysFeedbackEndpoints))]
    [InlineData(typeof(ChannelsMessagingEndpoints))]
    [InlineData(typeof(CommerceEndpoints))]
    [InlineData(typeof(MarketingEndpoints))]
    [InlineData(typeof(VoiceEndpoints))]
    [InlineData(typeof(AiGatewayEndpoints))]
    [InlineData(typeof(FinanceEndpoints))]
    [InlineData(typeof(DeveloperGatewayEndpoints))]
    [InlineData(typeof(DevEndpoints))]
    [InlineData(typeof(SeoEndpoints))]
    [InlineData(typeof(FunnelEndpoints))]
    [InlineData(typeof(DevGatewayEndpoints))]
    public void CoreEndpointClasses_Implement_IEndpointModule(Type moduleType)
    {
        typeof(IEndpointModule).IsAssignableFrom(moduleType).Should().BeTrue();
        moduleType.IsClass.Should().BeTrue();
        moduleType.IsAbstract.Should().BeFalse();
        moduleType.GetConstructor(Type.EmptyTypes).Should().NotBeNull();
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
}
