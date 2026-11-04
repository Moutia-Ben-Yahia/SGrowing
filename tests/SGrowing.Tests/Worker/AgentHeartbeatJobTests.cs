using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.Personas;
using SGrowing.Core.Entities;
using SGrowing.Worker.BackgroundJobs;
using Xunit;

namespace SGrowing.Tests.Worker;

public class AgentHeartbeatJobTests
{
    private (IServiceProvider Provider, string DbName) CreateServiceProvider(
        IAgentPersonaService? personaService = null)
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<SGrowingDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        if (personaService != null)
        {
            services.AddSingleton(personaService);
        }

        services.AddLogging();
        var provider = services.BuildServiceProvider();

        return (provider, dbName);
    }

    private SGrowingDbContext GetDbContext(IServiceProvider provider)
    {
        var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
    }

    [Fact]
    public async Task RunHeartbeatOnceAsync_WhenPersonaServiceNotRegistered_ReturnsZero()
    {
        var (provider, _) = CreateServiceProvider(); // no persona service
        var mockLogger = new Mock<ILogger<AgentHeartbeatJob>>();
        var job = new AgentHeartbeatJob(provider, mockLogger.Object);

        var executed = await job.RunHeartbeatOnceAsync();

        executed.Should().Be(0);
    }

    [Fact]
    public async Task RunHeartbeatOnceAsync_WithTargetTenantId_ExecutesForTargetTenantOnly()
    {
        var targetTenantId = Guid.NewGuid();
        var mockCycleResult = new AgentHeartbeatCycleResult(
            targetTenantId,
            AgentsEvaluatedCount: 3,
            AnomaliesDetectedCount: 0,
            Reports: new List<AgentHeartbeatRecord>(),
            CycleCompletedAtUtc: DateTime.UtcNow);

        var mockPersonaService = new Mock<IAgentPersonaService>();
        mockPersonaService
            .Setup(p => p.RunHeartbeatCycleAsync(targetTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCycleResult);

        var (provider, _) = CreateServiceProvider(personaService: mockPersonaService.Object);
        var mockLogger = new Mock<ILogger<AgentHeartbeatJob>>();
        var job = new AgentHeartbeatJob(provider, mockLogger.Object);

        var executed = await job.RunHeartbeatOnceAsync(targetTenantId);

        executed.Should().Be(1);
        mockPersonaService.Verify(p => p.RunHeartbeatCycleAsync(targetTenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunHeartbeatOnceAsync_WithTenantsInDatabase_IteratesAllTenants()
    {
        var tenant1 = new Tenant { Id = Guid.NewGuid(), Name = "Acme Corp", Slug = "acme" };
        var tenant2 = new Tenant { Id = Guid.NewGuid(), Name = "Globex Inc", Slug = "globex" };

        var mockPersonaService = new Mock<IAgentPersonaService>();
        mockPersonaService
            .Setup(p => p.RunHeartbeatCycleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tid, CancellationToken _) => new AgentHeartbeatCycleResult(
                tid,
                AgentsEvaluatedCount: 2,
                AnomaliesDetectedCount: 0,
                Reports: new List<AgentHeartbeatRecord>(),
                CycleCompletedAtUtc: DateTime.UtcNow));

        var (provider, _) = CreateServiceProvider(personaService: mockPersonaService.Object);

        using (var seedDb = GetDbContext(provider))
        {
            seedDb.Tenants.AddRange(tenant1, tenant2);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<AgentHeartbeatJob>>();
        var job = new AgentHeartbeatJob(provider, mockLogger.Object);

        var executed = await job.RunHeartbeatOnceAsync();

        executed.Should().Be(2);
        mockPersonaService.Verify(p => p.RunHeartbeatCycleAsync(tenant1.Id, It.IsAny<CancellationToken>()), Times.Once);
        mockPersonaService.Verify(p => p.RunHeartbeatCycleAsync(tenant2.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunHeartbeatOnceAsync_WhenTenantThrowsException_ContinuesRemainingTenants()
    {
        var tenant1 = new Tenant { Id = Guid.NewGuid(), Name = "Failing Tenant", Slug = "fail" };
        var tenant2 = new Tenant { Id = Guid.NewGuid(), Name = "Healthy Tenant", Slug = "healthy" };

        var mockPersonaService = new Mock<IAgentPersonaService>();
        mockPersonaService
            .Setup(p => p.RunHeartbeatCycleAsync(tenant1.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated AI cluster quota error"));

        mockPersonaService
            .Setup(p => p.RunHeartbeatCycleAsync(tenant2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentHeartbeatCycleResult(
                tenant2.Id,
                AgentsEvaluatedCount: 1,
                AnomaliesDetectedCount: 0,
                Reports: new List<AgentHeartbeatRecord>(),
                CycleCompletedAtUtc: DateTime.UtcNow));

        var (provider, _) = CreateServiceProvider(personaService: mockPersonaService.Object);

        using (var seedDb = GetDbContext(provider))
        {
            seedDb.Tenants.AddRange(tenant1, tenant2);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<AgentHeartbeatJob>>();
        var job = new AgentHeartbeatJob(provider, mockLogger.Object);

        var executed = await job.RunHeartbeatOnceAsync();

        // 1 of 2 succeeded despite the exception in tenant1
        executed.Should().Be(1);
        mockPersonaService.Verify(p => p.RunHeartbeatCycleAsync(tenant1.Id, It.IsAny<CancellationToken>()), Times.Once);
        mockPersonaService.Verify(p => p.RunHeartbeatCycleAsync(tenant2.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunHeartbeatOnceAsync_WhenNoTenantsInDatabase_FallsBackToDefaultTenant()
    {
        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var mockPersonaService = new Mock<IAgentPersonaService>();
        mockPersonaService
            .Setup(p => p.RunHeartbeatCycleAsync(defaultTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentHeartbeatCycleResult(
                defaultTenantId,
                AgentsEvaluatedCount: 1,
                AnomaliesDetectedCount: 0,
                Reports: new List<AgentHeartbeatRecord>(),
                CycleCompletedAtUtc: DateTime.UtcNow));

        var (provider, _) = CreateServiceProvider(personaService: mockPersonaService.Object);
        var mockLogger = new Mock<ILogger<AgentHeartbeatJob>>();
        var job = new AgentHeartbeatJob(provider, mockLogger.Object);

        var executed = await job.RunHeartbeatOnceAsync();

        executed.Should().Be(1);
        mockPersonaService.Verify(p => p.RunHeartbeatCycleAsync(defaultTenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunHeartbeatOnceAsync_WithCancellationToken_PropagatesCancellationToken()
    {
        var targetTenant = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var mockPersonaService = new Mock<IAgentPersonaService>();
        mockPersonaService
            .Setup(p => p.RunHeartbeatCycleAsync(targetTenant, token))
            .ReturnsAsync(new AgentHeartbeatCycleResult(targetTenant, 1, 0, new(), DateTime.UtcNow));

        var (provider, _) = CreateServiceProvider(personaService: mockPersonaService.Object);
        var mockLogger = new Mock<ILogger<AgentHeartbeatJob>>();
        var job = new AgentHeartbeatJob(provider, mockLogger.Object);

        var executed = await job.RunHeartbeatOnceAsync(targetTenant, token);

        executed.Should().Be(1);
        mockPersonaService.Verify(p => p.RunHeartbeatCycleAsync(targetTenant, token), Times.Once);
    }

    [Fact]
    public void Constructor_WithCustomCycleInterval_InstantiatesSuccessfully()
    {
        var (provider, _) = CreateServiceProvider();
        var mockLogger = new Mock<ILogger<AgentHeartbeatJob>>();
        var customInterval = TimeSpan.FromMinutes(45);

        var job = new AgentHeartbeatJob(provider, mockLogger.Object, customInterval);

        job.Should().NotBeNull();
    }

    [Fact]
    public async Task RunHeartbeatOnceAsync_WithMultipleSuccessfulTenants_ReturnsAccurateCount()
    {
        var tenantA = new Tenant { Id = Guid.NewGuid(), Name = "Alpha", Slug = "alpha" };
        var tenantB = new Tenant { Id = Guid.NewGuid(), Name = "Beta", Slug = "beta" };
        var tenantC = new Tenant { Id = Guid.NewGuid(), Name = "Gamma", Slug = "gamma" };

        var mockPersonaService = new Mock<IAgentPersonaService>();
        mockPersonaService
            .Setup(p => p.RunHeartbeatCycleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tid, CancellationToken _) => new AgentHeartbeatCycleResult(tid, 2, 0, new(), DateTime.UtcNow));

        var (provider, _) = CreateServiceProvider(personaService: mockPersonaService.Object);

        using (var seedDb = GetDbContext(provider))
        {
            seedDb.Tenants.AddRange(tenantA, tenantB, tenantC);
            await seedDb.SaveChangesAsync();
        }

        var mockLogger = new Mock<ILogger<AgentHeartbeatJob>>();
        var job = new AgentHeartbeatJob(provider, mockLogger.Object);

        var executed = await job.RunHeartbeatOnceAsync();

        executed.Should().Be(3);
    }
}
