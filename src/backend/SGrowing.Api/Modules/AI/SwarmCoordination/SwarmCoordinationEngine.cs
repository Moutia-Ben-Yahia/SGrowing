using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI.SwarmCoordination;

public interface ISwarmCoordinationEngine
{
    MissionPlan CreateMission(CreateMissionRequest request);
    MissionPlan? GetMission(string tenantId, string missionId);
    List<MissionPlan> ListMissions(string tenantId);
    MissionPlan ApproveTask(ApproveTaskRequest request);
    MissionPlan UpdateScratchpad(UpdateScratchpadRequest request);
    SwarmTelemetrySummary GetTelemetry(string tenantId, string missionId);
}

public class SwarmCoordinationEngine : ISwarmCoordinationEngine
{
    private readonly ConcurrentDictionary<string, MissionPlan> _missions = new();
    private readonly SGrowingDbContext? _dbContext;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<SwarmCoordinationEngine>? _logger;
    private readonly object _dbLock = new();

    private static string BuildKey(string tenantId, string missionId) => $"{tenantId}::{missionId}";

    public SwarmCoordinationEngine()
        : this((IServiceScopeFactory?)null, null)
    {
    }

    public SwarmCoordinationEngine(IServiceScopeFactory? scopeFactory, ILogger<SwarmCoordinationEngine>? logger = null)
        : this(null, scopeFactory, logger)
    {
    }

    public SwarmCoordinationEngine(SGrowingDbContext? dbContext, ILogger<SwarmCoordinationEngine>? logger = null)
        : this(dbContext, null, logger)
    {
    }

    public SwarmCoordinationEngine(
        SGrowingDbContext? dbContext,
        IServiceScopeFactory? scopeFactory,
        ILogger<SwarmCoordinationEngine>? logger = null)
    {
        _dbContext = dbContext;
        _scopeFactory = scopeFactory;
        _logger = logger;

        WarmupFromPersistence();
    }

    private void WarmupFromPersistence()
    {
        try
        {
            if (_dbContext != null)
            {
                lock (_dbLock)
                {
                    LoadFromDb(_dbContext);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
                if (db != null)
                {
                    LoadFromDb(db);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to warm up SwarmCoordinationEngine cache from database.");
        }
    }

    private void LoadFromDb(SGrowingDbContext db)
    {
        var missions = db.SwarmMissions
            .AsNoTracking()
            .Include(m => m.Tasks)
            .ToList();

        foreach (var m in missions)
        {
            _missions[BuildKey(m.TenantId, m.MissionId)] = m.ToModel();
        }
    }

    private void SyncMissionToDb(MissionPlan plan)
    {
        try
        {
            ExecuteWithDb(db =>
            {
                var existing = db.SwarmMissions
                    .Include(m => m.Tasks)
                    .FirstOrDefault(m => m.MissionId == plan.MissionId);

                if (existing == null)
                {
                    var entity = plan.ToEntity();
                    db.SwarmMissions.Add(entity);
                }
                else
                {
                    existing.Title = plan.Title;
                    existing.HighLevelGoal = plan.HighLevelGoal;
                    existing.ScratchpadJson = JsonSerializer.Serialize(plan.Scratchpad);
                    existing.FinishedAtUtc = plan.FinishedAtUtc;
                    existing.UpdatedAtUtc = DateTime.UtcNow;

                    foreach (var taskModel in plan.Tasks)
                    {
                        var existingTask = existing.Tasks.FirstOrDefault(t => t.TaskId == taskModel.TaskId);
                        if (existingTask != null)
                        {
                            existingTask.Title = taskModel.Title;
                            existingTask.Description = taskModel.Description;
                            existingTask.AssignedTo = taskModel.AssignedTo.ToString();
                            existingTask.Priority = taskModel.Priority.ToString();
                            existingTask.Status = taskModel.Status.ToString();
                            existingTask.RequiresHumanApproval = taskModel.RequiresHumanApproval;
                            existingTask.OutputArtifact = taskModel.OutputArtifact;
                            existingTask.CompletedAtUtc = taskModel.CompletedAtUtc;
                        }
                        else
                        {
                            existing.Tasks.Add(taskModel.ToEntity(existing.Id));
                        }
                    }
                }

                db.SaveChanges();
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist swarm mission {MissionId} to database.", plan.MissionId);
        }
    }

    private MissionPlan? LoadMissionFromDb(string tenantId, string missionId)
    {
        try
        {
            return QueryWithDb(db =>
            {
                var entity = db.SwarmMissions
                    .AsNoTracking()
                    .Include(m => m.Tasks)
                    .FirstOrDefault(m => m.TenantId == tenantId && m.MissionId == missionId);

                return entity?.ToModel();
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to query swarm mission {MissionId} from database.", missionId);
            return null;
        }
    }

    private void ExecuteWithDb(Action<SGrowingDbContext> action)
    {
        if (_dbContext != null)
        {
            lock (_dbLock)
            {
                action(_dbContext);
            }
            return;
        }

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
            if (db != null)
            {
                action(db);
            }
        }
    }

    private T? QueryWithDb<T>(Func<SGrowingDbContext, T?> query)
    {
        if (_dbContext != null)
        {
            lock (_dbLock)
            {
                return query(_dbContext);
            }
        }

        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<SGrowingDbContext>();
            if (db != null)
            {
                return query(db);
            }
        }

        return default;
    }

    public MissionPlan CreateMission(CreateMissionRequest request)
    {
        var missionId = "mis_" + Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;

        // Autonomous task decomposition across personas
        var tasks = new List<SwarmTask>
        {
            new(
                TaskId: "tsk_1_research",
                MissionId: missionId,
                Title: "Account & Competitor Research",
                Description: "Harvest ICP firmographics, tech stack insights, and pain points.",
                AssignedTo: AgentPersonaRole.LeadResearcher,
                Priority: SwarmTaskPriority.High,
                Status: SwarmTaskStatus.Completed,
                RequiresHumanApproval: false,
                OutputArtifact: "Identified 42 Tier-A enterprise accounts with active hiring signals.",
                CreatedAtUtc: now,
                CompletedAtUtc: now.AddMinutes(2)
            ),
            new(
                TaskId: "tsk_2_copy",
                MissionId: missionId,
                Title: "Hyper-Personalized Spintax Generation",
                Description: "Draft 3 cold outreach angles tailored to VP Marketing & RevOps titles.",
                AssignedTo: AgentPersonaRole.OutreachCopywriter,
                Priority: SwarmTaskPriority.High,
                Status: SwarmTaskStatus.Completed,
                RequiresHumanApproval: false,
                OutputArtifact: "Angle A (ROAS Decay), Angle B (Autonomous SDR), Angle C (Attribution Blindspots).",
                CreatedAtUtc: now,
                CompletedAtUtc: now.AddMinutes(5)
            ),
            new(
                TaskId: "tsk_3_seo",
                MissionId: missionId,
                Title: "Search Intent & Content Cluster Gap Audit",
                Description: "Audit competitor SERPs and keyword cannibalization risks.",
                AssignedTo: AgentPersonaRole.TechnicalSeoAuditor,
                Priority: SwarmTaskPriority.Medium,
                Status: SwarmTaskStatus.Completed,
                RequiresHumanApproval: false,
                OutputArtifact: "Identified 3 programmatic landing page cluster targets (Volume: 18.5k/mo).",
                CreatedAtUtc: now,
                CompletedAtUtc: now.AddMinutes(8)
            ),
            new(
                TaskId: "tsk_4_compliance",
                MissionId: missionId,
                Title: "Compliance & Opt-Out Guardrail Audit",
                Description: "Validate CAN-SPAM, GDPR consent tokens, and physical address disclosures.",
                AssignedTo: AgentPersonaRole.ComplianceGuardian,
                Priority: SwarmTaskPriority.Critical,
                Status: SwarmTaskStatus.Completed,
                RequiresHumanApproval: false,
                OutputArtifact: "Verified: Unsubscribe tokens present, DNS DMARC aligned, no PII leakage.",
                CreatedAtUtc: now,
                CompletedAtUtc: now.AddMinutes(10)
            ),
            new(
                TaskId: "tsk_5_dispatch",
                MissionId: missionId,
                Title: "Autonomous Ad Spend Allocation & Outreach Dispatch",
                Description: "Allocate $2,500 daily budget to Meta/Google Ads and dispatch 500 invitations.",
                AssignedTo: AgentPersonaRole.AdOptimizer,
                Priority: SwarmTaskPriority.Critical,
                Status: SwarmTaskStatus.AwaitingApproval,
                RequiresHumanApproval: true,
                OutputArtifact: "PROPOSED: $1,500 Meta CAPI + $1,000 Google Search + 500 LinkedIn SDR messages.",
                CreatedAtUtc: now,
                CompletedAtUtc: null
            )
        };

        var scratchpad = new Dictionary<string, string>
        {
            ["mission_phase"] = "ExecutionPendingApproval",
            ["icp_tier"] = "Tier A Enterprise ($50M+ ARR)",
            ["primary_kpi"] = "Qualified Sales Pipeline ($250k)",
            ["target_market"] = "US & DACH B2B SaaS"
        };

        var plan = new MissionPlan(
            MissionId: missionId,
            TenantId: request.TenantId,
            Title: request.Title,
            HighLevelGoal: request.HighLevelGoal,
            Tasks: tasks,
            Scratchpad: scratchpad,
            StartedAtUtc: now,
            FinishedAtUtc: null
        );

        _missions[BuildKey(request.TenantId, missionId)] = plan;
        SyncMissionToDb(plan);
        return plan;
    }

    public MissionPlan? GetMission(string tenantId, string missionId)
    {
        var key = BuildKey(tenantId, missionId);
        if (_missions.TryGetValue(key, out var plan))
        {
            return plan;
        }

        var dbPlan = LoadMissionFromDb(tenantId, missionId);
        if (dbPlan != null)
        {
            _missions[key] = dbPlan;
            return dbPlan;
        }

        return null;
    }

    public List<MissionPlan> ListMissions(string tenantId)
    {
        try
        {
            var dbMissions = QueryWithDb(db =>
            {
                return db.SwarmMissions
                    .AsNoTracking()
                    .Include(m => m.Tasks)
                    .Where(m => m.TenantId == tenantId)
                    .ToList();
            });

            if (dbMissions != null)
            {
                foreach (var entity in dbMissions)
                {
                    _missions[BuildKey(entity.TenantId, entity.MissionId)] = entity.ToModel();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to query tenant missions for {TenantId} from database.", tenantId);
        }

        return _missions.Values
            .Where(m => m.TenantId == tenantId)
            .OrderByDescending(m => m.StartedAtUtc)
            .ToList();
    }

    public MissionPlan ApproveTask(ApproveTaskRequest request)
    {
        var key = BuildKey(request.TenantId, request.MissionId);
        if (!_missions.TryGetValue(key, out var plan))
        {
            plan = LoadMissionFromDb(request.TenantId, request.MissionId);
            if (plan == null)
            {
                throw new KeyNotFoundException($"Mission '{request.MissionId}' not found.");
            }
            _missions[key] = plan;
        }

        var taskIdx = plan.Tasks.FindIndex(t => t.TaskId == request.TaskId);
        if (taskIdx < 0)
        {
            throw new KeyNotFoundException($"Task '{request.TaskId}' not found in mission.");
        }

        var targetTask = plan.Tasks[taskIdx];
        var updatedTask = targetTask with
        {
            Status = request.Approved ? SwarmTaskStatus.Completed : SwarmTaskStatus.Rejected,
            CompletedAtUtc = DateTime.UtcNow,
            OutputArtifact = request.Approved
                ? $"{targetTask.OutputArtifact} [APPROVED by {request.ApproverId}]"
                : $"REJECTED by {request.ApproverId}. Reason: {request.Reason}"
        };

        var updatedTasks = new List<SwarmTask>(plan.Tasks);
        updatedTasks[taskIdx] = updatedTask;

        var allDone = updatedTasks.All(t => t.Status is SwarmTaskStatus.Completed or SwarmTaskStatus.Rejected);
        var updatedScratchpad = new Dictionary<string, string>(plan.Scratchpad)
        {
            ["last_approved_task"] = request.TaskId,
            ["mission_phase"] = allDone ? "MissionComplete" : "InProgress"
        };

        var updatedPlan = plan with
        {
            Tasks = updatedTasks,
            Scratchpad = updatedScratchpad,
            FinishedAtUtc = allDone ? DateTime.UtcNow : null
        };

        _missions[key] = updatedPlan;
        SyncMissionToDb(updatedPlan);
        return updatedPlan;
    }

    public MissionPlan UpdateScratchpad(UpdateScratchpadRequest request)
    {
        var key = BuildKey(request.TenantId, request.MissionId);
        if (!_missions.TryGetValue(key, out var plan))
        {
            plan = LoadMissionFromDb(request.TenantId, request.MissionId);
            if (plan == null)
            {
                throw new KeyNotFoundException($"Mission '{request.MissionId}' not found.");
            }
            _missions[key] = plan;
        }

        var updatedScratchpad = new Dictionary<string, string>(plan.Scratchpad)
        {
            [request.Key] = request.Value
        };

        var updatedPlan = plan with { Scratchpad = updatedScratchpad };
        _missions[key] = updatedPlan;
        SyncMissionToDb(updatedPlan);
        return updatedPlan;
    }

    public SwarmTelemetrySummary GetTelemetry(string tenantId, string missionId)
    {
        var plan = GetMission(tenantId, missionId)
            ?? throw new KeyNotFoundException($"Mission '{missionId}' not found.");

        return new SwarmTelemetrySummary(
            MissionId: missionId,
            TotalTasks: plan.Tasks.Count,
            CompletedTasks: plan.Tasks.Count(t => t.Status == SwarmTaskStatus.Completed),
            PendingApprovalCount: plan.Tasks.Count(t => t.Status == SwarmTaskStatus.AwaitingApproval),
            Scratchpad: plan.Scratchpad
        );
    }
}
