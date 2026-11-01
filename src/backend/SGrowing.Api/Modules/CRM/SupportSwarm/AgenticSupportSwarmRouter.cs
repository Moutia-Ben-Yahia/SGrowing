namespace SGrowing.Api.Modules.CRM.SupportSwarm;

/// <summary>
/// Defines the autonomous omnichannel AI fleet swarm router interface.
/// </summary>
public interface IAgenticSupportSwarmRouter
{
    RouteTicketResponse RouteTicket(CustomerSupportTicket ticket);
    CustomerSupportTicket ReassignTicket(string ticketId, AgentPersona newPersona, string reason);
    CustomerSupportTicket? GetTicket(string ticketId);
    IReadOnlyList<AgentWorkload> GetAgentWorkloadSummary();
    CustomerSupportTicket ResolveTicket(string ticketId, string? notes = null);
    IReadOnlyList<CustomerSupportTicket> GetAllTickets();
    IReadOnlyList<CustomerSupportTicket> GetTicketsByStatus(TicketStatus status);
}

/// <summary>
/// High-throughput, thread-safe autonomous ticket routing engine using native .NET System.Threading.Lock.
/// Evaluates customer lifetime value (LTV), sentiment severity, domain category, and persona capacity.
/// </summary>
public sealed class AgenticSupportSwarmRouter : IAgenticSupportSwarmRouter
{
    private const decimal VipLtvThresholdDzd = 150_000m;
    private const double SevereNegativeSentimentThreshold = -0.60;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, CustomerSupportTicket> _tickets = [];
    private readonly Dictionary<AgentPersona, int> _activeCounts = [];
    private readonly Dictionary<AgentPersona, int> _capacities = [];

    public AgenticSupportSwarmRouter(
        int logisticsCapacity = 100,
        int billingCapacity = 80,
        int productAdvisorCapacity = 80,
        int supervisorCapacity = 30)
    {
        _capacities[AgentPersona.LogisticsDispatcherAi] = logisticsCapacity;
        _capacities[AgentPersona.BillingSpecialistAi] = billingCapacity;
        _capacities[AgentPersona.ProductAdvisorAi] = productAdvisorCapacity;
        _capacities[AgentPersona.SeniorHumanSupervisor] = supervisorCapacity;

        foreach (AgentPersona persona in Enum.GetValues<AgentPersona>())
        {
            _activeCounts[persona] = 0;
            if (!_capacities.ContainsKey(persona))
            {
                _capacities[persona] = 50;
            }
        }
    }

    /// <summary>
    /// Routes an incoming support ticket to the optimal agent persona using swarm intelligence rules.
    /// </summary>
    public RouteTicketResponse RouteTicket(CustomerSupportTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (ticket.SentimentScore is < -1.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticket), "Sentiment score must be within [-1.0, 1.0].");
        }

        if (ticket.CustomerLtvDzd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticket), "Customer LTV cannot be negative.");
        }

        var normalizedTicketId = string.IsNullOrWhiteSpace(ticket.TicketId)
            ? $"TCK-{Guid.NewGuid():N}"
            : ticket.TicketId;

        lock (_lock)
        {
            AgentPersona assignedPersona;
            TicketPriority finalPriority = ticket.Priority;
            TicketStatus finalStatus;
            string routingReason;

            // 1. VIP Customer Escalation: LTV > 150k DZD or Priority == UrgentVip -> SeniorHumanSupervisor
            if (ticket.CustomerLtvDzd > VipLtvThresholdDzd || ticket.Priority == TicketPriority.UrgentVip)
            {
                assignedPersona = AgentPersona.SeniorHumanSupervisor;
                finalPriority = TicketPriority.UrgentVip;
                finalStatus = TicketStatus.Escalated;
                routingReason = ticket.CustomerLtvDzd > VipLtvThresholdDzd
                    ? $"VIP Customer Escalation: LTV ({ticket.CustomerLtvDzd:N0} DZD > {VipLtvThresholdDzd:N0} DZD) routed directly to SeniorHumanSupervisor."
                    : "Urgent VIP priority ticket routed directly to SeniorHumanSupervisor.";
            }
            // 2. Severe Negative Sentiment Escalation: SentimentScore <= -0.60
            else if (ticket.SentimentScore <= SevereNegativeSentimentThreshold)
            {
                assignedPersona = AgentPersona.SeniorHumanSupervisor;
                finalPriority = ticket.Priority < TicketPriority.High ? TicketPriority.High : ticket.Priority;
                finalStatus = TicketStatus.Escalated;
                routingReason = $"Severe negative sentiment ({ticket.SentimentScore:F2} <= {SevereNegativeSentimentThreshold:F2}) escalated to SeniorHumanSupervisor with elevated priority.";
            }
            // 3. Domain Specialist AI Persona Routing
            else
            {
                assignedPersona = ticket.Category switch
                {
                    TicketCategory.OrderDeliveryLogistics => AgentPersona.LogisticsDispatcherAi,
                    TicketCategory.BillingAndRefunds => AgentPersona.BillingSpecialistAi,
                    TicketCategory.TechnicalProduct or TicketCategory.WholesaleB2B => AgentPersona.ProductAdvisorAi,
                    TicketCategory.AccountAndSecurity => AgentPersona.SeniorHumanSupervisor,
                    _ => AgentPersona.ProductAdvisorAi
                };

                finalStatus = assignedPersona == AgentPersona.SeniorHumanSupervisor
                    ? TicketStatus.Escalated
                    : TicketStatus.InProgress;

                routingReason = $"Domain specialist persona '{assignedPersona}' assigned for ticket category '{ticket.Category}'.";
            }

            // 4. Capacity constraints enforcement
            var currentLoad = _activeCounts[assignedPersona];
            var capacity = _capacities[assignedPersona];

            if (currentLoad >= capacity)
            {
                if (assignedPersona != AgentPersona.SeniorHumanSupervisor &&
                    _activeCounts[AgentPersona.SeniorHumanSupervisor] < _capacities[AgentPersona.SeniorHumanSupervisor])
                {
                    var fallbackPersona = AgentPersona.SeniorHumanSupervisor;
                    routingReason += $" [Capacity Alert: {assignedPersona} load ({currentLoad}/{capacity}) reached. Overflow routed to {fallbackPersona}].";
                    assignedPersona = fallbackPersona;
                    finalStatus = TicketStatus.Escalated;
                }
                else
                {
                    finalStatus = TicketStatus.Open;
                    routingReason += $" [Capacity Alert: {assignedPersona} max capacity ({capacity}) reached. Ticket queued as Open].";
                }
            }

            // Update workload counters
            _activeCounts[assignedPersona]++;

            var routedTicket = ticket with
            {
                TicketId = normalizedTicketId,
                AssignedPersona = assignedPersona,
                Priority = finalPriority,
                Status = finalStatus,
                EscalationReason = finalStatus == TicketStatus.Escalated ? routingReason : ticket.EscalationReason,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _tickets[normalizedTicketId] = routedTicket;

            return new RouteTicketResponse(
                TicketId: normalizedTicketId,
                AssignedPersona: assignedPersona,
                FinalPriority: finalPriority,
                Status: finalStatus,
                RoutingReason: routingReason,
                AssignedAtUtc: DateTime.UtcNow,
                Ticket: routedTicket
            );
        }
    }

    /// <summary>
    /// Reassigns an existing ticket to a different persona and adjusts fleet workload counters.
    /// </summary>
    public CustomerSupportTicket ReassignTicket(string ticketId, AgentPersona newPersona, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        lock (_lock)
        {
            if (!_tickets.TryGetValue(ticketId, out var existingTicket))
            {
                throw new KeyNotFoundException($"Support ticket '{ticketId}' was not found.");
            }

            if (existingTicket.Status == TicketStatus.Resolved)
            {
                throw new InvalidOperationException($"Cannot reassign resolved ticket '{ticketId}'.");
            }

            var oldPersona = existingTicket.AssignedPersona;
            if (_activeCounts[oldPersona] > 0)
            {
                _activeCounts[oldPersona]--;
            }

            _activeCounts[newPersona]++;

            var updatedTicket = existingTicket with
            {
                AssignedPersona = newPersona,
                Status = newPersona == AgentPersona.SeniorHumanSupervisor ? TicketStatus.Escalated : TicketStatus.InProgress,
                ReassignedReason = reason,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _tickets[ticketId] = updatedTicket;
            return updatedTicket;
        }
    }

    /// <summary>
    /// Retrieves a ticket by its identifier.
    /// </summary>
    public CustomerSupportTicket? GetTicket(string ticketId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);

        lock (_lock)
        {
            return _tickets.GetValueOrDefault(ticketId);
        }
    }

    /// <summary>
    /// Returns real-time workload and availability status across all personas.
    /// </summary>
    public IReadOnlyList<AgentWorkload> GetAgentWorkloadSummary()
    {
        lock (_lock)
        {
            List<AgentWorkload> workloads = [];
            foreach (AgentPersona persona in Enum.GetValues<AgentPersona>())
            {
                var active = _activeCounts.GetValueOrDefault(persona, 0);
                var maxCap = _capacities.GetValueOrDefault(persona, 50);
                workloads.Add(new AgentWorkload(
                    Persona: persona,
                    ActiveTicketsCount: active,
                    MaxCapacity: maxCap,
                    IsAvailable: active < maxCap
                ));
            }
            return workloads.AsReadOnly();
        }
    }

    /// <summary>
    /// Marks a ticket as resolved and decrements the active workload count for the assigned persona.
    /// </summary>
    public CustomerSupportTicket ResolveTicket(string ticketId, string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);

        lock (_lock)
        {
            if (!_tickets.TryGetValue(ticketId, out var existingTicket))
            {
                throw new KeyNotFoundException($"Support ticket '{ticketId}' was not found.");
            }

            if (existingTicket.Status != TicketStatus.Resolved)
            {
                if (_activeCounts[existingTicket.AssignedPersona] > 0)
                {
                    _activeCounts[existingTicket.AssignedPersona]--;
                }
            }

            var resolvedTicket = existingTicket with
            {
                Status = TicketStatus.Resolved,
                ResolutionNotes = notes ?? existingTicket.ResolutionNotes,
                ResolvedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _tickets[ticketId] = resolvedTicket;
            return resolvedTicket;
        }
    }

    /// <summary>
    /// Retrieves all tracked support tickets.
    /// </summary>
    public IReadOnlyList<CustomerSupportTicket> GetAllTickets()
    {
        lock (_lock)
        {
            return [.. _tickets.Values];
        }
    }

    /// <summary>
    /// Retrieves tickets filtered by status.
    /// </summary>
    public IReadOnlyList<CustomerSupportTicket> GetTicketsByStatus(TicketStatus status)
    {
        lock (_lock)
        {
            return [.. _tickets.Values.Where(t => t.Status == status)];
        }
    }
}
