# Story 22 — Automatic assignment (Story: SA-2)

---

## Prerequisites

- Story 21 completed: [`21-story-SA-1.md`](21-story-SA-1.md) — not a hard data dependency, but establishes the `Application/Sla` vs `Application/Tickets` folder convention this story follows (the rule engine lives in `Tickets/`, next to `TicketAssignmentService`, since it calls it directly).
- Ticket Management Story 07 completed ([`../ticket-management/07-story-TM-3.md`](../ticket-management/07-story-TM-3.md)) — provides `Agent`, `Team`, `TicketAssignmentService.AssignAsync`/`GetAgentLoadAsync`, and `IAssignmentNotifier`. This story's rule engine calls `AssignAsync` directly rather than duplicating assignment/audit/notification logic.
- Agent Dashboard Story 20 completed ([`../agent-dashboard/20-story-AD-5.md`](../agent-dashboard/20-story-AD-5.md)) — provides `AgentNotificationService.NotifyAsync`, the shared mechanism this story reuses to notify an auto-assigned agent in-app immediately (its own doc comment: "the one shared way any part of this app creates an agent notification. Do not add a second, parallel mechanism").

---

## Story Goal

1. Assignment **rules** can be configured, each matching on any combination of ticket category, channel, or language, and routing either to a fixed team or to the least-loaded currently-available agent who has a required skill.
2. Rules are evaluated in a configurable order; the first whose conditions all match wins.
3. Workload balancing reuses Story 07's `TicketAssignmentService.GetAgentLoadAsync` open-ticket counts — never a separate load query.
4. A ticket that matches no active rule (or whose matched skill-based rule has no available skilled agent) falls back to a seeded default "General Queue" team rather than being left unassigned.
5. When a rule resolves to a specific agent, that agent gets an immediate in-app notification via the existing `AgentNotificationService`.
6. New tickets are evaluated automatically at creation time (`TicketService.CreateAsync`).

**Assumption (no skills-matrix or language-detection system exists yet):** `Agent` skills/languages are minimal normalized tag tables (mirroring `ContactDetail`'s per-customer rows), and `Ticket.Language` is a plain nullable field set by the caller at creation — not detected from message content. Flag both explicitly as stand-ins.

**Not in scope:** SLA target configuration (Story 21) and escalation rules (Story 23) — this story only decides *who* a new ticket goes to. A full skills-matrix/workforce-management module. A drag-and-drop rule builder UI — a simple ordered rule list is sufficient.

---

## Context — Read These Files First

1. [`../ticket-management/07-story-TM-3.md`](../ticket-management/07-story-TM-3.md), `## Backend Tasks` → `### 2` — `TicketAssignmentService.AssignAsync`'s exact signature and behavior (mutual-exclusivity check, audit entry, notifier call); this story's engine calls it as-is.
2. `src/SupportCrm.Application/Tickets/TicketAssignmentService.cs`, lines 1–45 (whole file) — `AssignAsync` (lines 12–28) and `GetAgentLoadAsync` (lines 30–38), reused directly by the new rule engine.
3. `src/SupportCrm.Application/Tickets/AgentNotificationService.cs`, lines 30–34 — `NotifyAsync(agentId, kind, message, relatedTicketId, ct)`, the exact call this story's engine makes for the "notify agent immediately" AC.
4. `src/SupportCrm.Application/Tickets/TicketService.cs`, lines 1–32 — `CreateAsync`; this story adds one call after its existing `SaveChangesAsync` (line 29) to trigger auto-assignment on every new ticket.
5. `src/SupportCrm.Domain/Entities/Agent.cs` (all 25 lines) — `IsAvailable` (line 9) is the availability filter for skill-based routing; `Team.cs` (all 17 lines) — the shape the new seeded "General Queue" fallback follows.
6. `src/SupportCrm.Domain/Entities/ContactDetail.cs` — read via `grep -n "class ContactDetail" src/SupportCrm.Domain/Entities/ContactDetail.cs` first to confirm the exact shape, then read it — precedent for a simple `(Id, OwnerId, Value)`-style child table, which `AgentSkill`/`AgentLanguage` follow.
7. `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`, lines 148–153 (`Team` block) and lines 116–129 (`TicketCategory`'s `HasData` seeding) — precedent for this story's seeded "General Queue" `Team` row.
8. `src/SupportCrm.Domain/Repositories/ITicketRepository.cs`, line 24 (`CountOpenGroupedByAgentAsync`) — the existing workload signal this story's engine reuses, not duplicates.

---

## Backend Tasks

### 1 — Domain: `AgentSkill`, `AgentLanguage`, `Ticket.Language`, `AssignmentRule`

**Create file: `src/SupportCrm.Domain/Entities/AgentSkill.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class AgentSkill
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public string Skill { get; private set; } = default!;

    private AgentSkill() { } // EF Core

    public AgentSkill(Guid agentId, string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            throw new ArgumentException("Skill is required.", nameof(skill));
        Id = Guid.NewGuid();
        AgentId = agentId;
        Skill = skill;
    }
}
```

**Create file: `src/SupportCrm.Domain/Entities/AgentLanguage.cs`** — identical shape, `Language` instead of `Skill`.

**File: `src/SupportCrm.Domain/Entities/Ticket.cs`** — add a property after `LastEscalatedAtUtc` (line 20):

```csharp
    public string? Language { get; private set; }
```

and a setter after `MarkEscalated` (after line 64):

```csharp

    public void SetLanguage(string? language) => Language = language;
```

**Create file: `src/SupportCrm.Domain/Entities/AssignmentRule.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// Rules are evaluated in SortOrder order by AssignmentRuleEngine; the first whose
// MatchesConditions() passes wins. Exactly one of RequiredSkill / TargetTeamId is set:
// a rule either routes straight to a fixed team, or routes to the least-loaded *available*
// agent who has RequiredSkill (workload via TicketAssignmentService.GetAgentLoadAsync).
public class AssignmentRule
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public Guid? CategoryId { get; private set; }
    public TicketChannel? Channel { get; private set; }
    public string? Language { get; private set; }
    public string? RequiredSkill { get; private set; }
    public Guid? TargetTeamId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private AssignmentRule() { } // EF Core

    public AssignmentRule(string name, int sortOrder, Guid? categoryId, TicketChannel? channel, string? language, string? requiredSkill, Guid? targetTeamId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (categoryId is null && channel is null && language is null)
            throw new ArgumentException("A rule must match on at least one of category, channel, or language.", nameof(categoryId));
        if ((requiredSkill is null) == (targetTeamId is null))
            throw new ArgumentException("A rule must target exactly one of a required skill or a team.", nameof(targetTeamId));

        Id = Guid.NewGuid();
        Name = name;
        SortOrder = sortOrder;
        CategoryId = categoryId;
        Channel = channel;
        Language = language;
        RequiredSkill = requiredSkill;
        TargetTeamId = targetTeamId;
    }

    public void Deactivate() => IsActive = false;

    public bool MatchesConditions(Guid? ticketCategoryId, TicketChannel ticketChannel, string? ticketLanguage) =>
        (CategoryId is null || CategoryId == ticketCategoryId) &&
        (Channel is null || Channel == ticketChannel) &&
        (Language is null || string.Equals(Language, ticketLanguage, StringComparison.OrdinalIgnoreCase));
}
```

**Extend file: `src/SupportCrm.Domain/Repositories/IAgentRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<Agent>> GetBySkillAsync(string skill, CancellationToken ct);
    Task AddSkillAsync(Guid agentId, string skill, CancellationToken ct);
    Task<IReadOnlyList<string>> GetSkillsAsync(Guid agentId, CancellationToken ct);
    Task AddLanguageAsync(Guid agentId, string language, CancellationToken ct);
    Task<IReadOnlyList<string>> GetLanguagesAsync(Guid agentId, CancellationToken ct);
```

**Create file: `src/SupportCrm.Domain/Repositories/IAssignmentRuleRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IAssignmentRuleRepository
{
    Task<IReadOnlyList<AssignmentRule>> GetActiveOrderedAsync(CancellationToken ct);
    Task AddAsync(AssignmentRule rule, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, `AgentService` additions, `AssignmentRuleService`, `AssignmentRuleEngine`

**Create file: `src/SupportCrm.Application/Tickets/AssignmentRuleDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record CreateAssignmentRuleRequest(string Name, int SortOrder, Guid? CategoryId, TicketChannel? Channel, string? Language, string? RequiredSkill, Guid? TargetTeamId);
public record AssignmentRuleDto(Guid Id, string Name, int SortOrder, Guid? CategoryId, TicketChannel? Channel, string? Language, string? RequiredSkill, Guid? TargetTeamId);
public record AddAgentSkillRequest(string Skill);
public record AddAgentLanguageRequest(string Language);
```

**File: `src/SupportCrm.Application/Tickets/TicketDtos.cs`** — extend `CreateTicketRequest` (lines 5–11) with one more field:

```csharp
public record CreateTicketRequest(
    TicketChannel Channel,
    string Subject,
    string? Description,
    string RequesterName,
    string? RequesterContactValue,
    string CreatedBy,
    string? Language = null);
```

**File: `src/SupportCrm.Application/Tickets/AgentService.cs`** — add after `SetSensitiveDataAccessAsync` (after line 31):

```csharp

    public Task AddSkillAsync(Guid agentId, string skill, CancellationToken ct) => repository.AddSkillAsync(agentId, skill.Trim(), ct);

    public Task<IReadOnlyList<string>> GetSkillsAsync(Guid agentId, CancellationToken ct) => repository.GetSkillsAsync(agentId, ct);

    public Task AddLanguageAsync(Guid agentId, string language, CancellationToken ct) => repository.AddLanguageAsync(agentId, language.Trim(), ct);

    public Task<IReadOnlyList<string>> GetLanguagesAsync(Guid agentId, CancellationToken ct) => repository.GetLanguagesAsync(agentId, ct);
```

**Create file: `src/SupportCrm.Application/Tickets/AssignmentRuleService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AssignmentRuleService(IAssignmentRuleRepository repository)
{
    public async Task<AssignmentRuleDto> CreateAsync(CreateAssignmentRuleRequest request, CancellationToken ct)
    {
        var rule = new AssignmentRule(request.Name.Trim(), request.SortOrder, request.CategoryId, request.Channel, request.Language, request.RequiredSkill, request.TargetTeamId);
        await repository.AddAsync(rule, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<IReadOnlyList<AssignmentRuleDto>> GetActiveOrderedAsync(CancellationToken ct) =>
        (await repository.GetActiveOrderedAsync(ct)).Select(ToDto).ToList();

    private static AssignmentRuleDto ToDto(AssignmentRule r) => new(r.Id, r.Name, r.SortOrder, r.CategoryId, r.Channel, r.Language, r.RequiredSkill, r.TargetTeamId);
}
```

**Create file: `src/SupportCrm.Application/Tickets/AssignmentRuleEngine.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AssignmentRuleEngine(
    ITicketRepository ticketRepository,
    IAgentRepository agentRepository,
    IAssignmentRuleRepository ruleRepository,
    TicketAssignmentService assignmentService,
    AgentNotificationService notificationService)
{
    // Fixed seed id for the "General Queue" team (see SupportCrmDbContext's Team HasData) —
    // the fallback target when no active rule matches, or a skill-based rule has no available
    // skilled agent, so a ticket always lands somewhere instead of being silently unassigned.
    public static readonly Guid DefaultQueueTeamId = new("33333333-3333-3333-3333-333333333301");

    public async Task EvaluateAndAssignAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var rules = await ruleRepository.GetActiveOrderedAsync(ct);
        var rule = rules.FirstOrDefault(r => r.MatchesConditions(ticket.CategoryId, ticket.Channel, ticket.Language));

        Guid? targetAgentId = null;
        Guid? targetTeamId = DefaultQueueTeamId;

        if (rule is not null)
        {
            if (rule.TargetTeamId is not null)
            {
                targetTeamId = rule.TargetTeamId;
            }
            else
            {
                var candidate = await PickLeastLoadedSkilledAgentAsync(rule.RequiredSkill!, ct);
                if (candidate is not null)
                {
                    targetAgentId = candidate.Id;
                    targetTeamId = null;
                }
                // else: no available skilled agent right now — falls through to DefaultQueueTeamId.
            }
        }

        await assignmentService.AssignAsync(ticketId, new AssignTicketRequest(targetAgentId, targetTeamId, "System"), ct);

        if (targetAgentId is not null)
        {
            await notificationService.NotifyAsync(targetAgentId.Value, "AutoAssigned",
                $"Ticket {ticket.ReferenceNumber} was auto-assigned to you.", ticketId, ct);
        }
    }

    private async Task<Agent?> PickLeastLoadedSkilledAgentAsync(string requiredSkill, CancellationToken ct)
    {
        var skilled = await agentRepository.GetBySkillAsync(requiredSkill, ct);
        var available = skilled.Where(a => a.IsAvailable).ToList();
        if (available.Count == 0) return null;

        var load = await ticketRepository.CountOpenGroupedByAgentAsync(ct);
        return available.OrderBy(a => load.GetValueOrDefault(a.Id, 0)).First();
    }
}
```

**File: `src/SupportCrm.Application/Tickets/TicketService.cs`** — add `AssignmentRuleEngine assignmentRuleEngine` to the primary constructor's parameter list (line 6–10) and call it at the end of `CreateAsync`, after `SaveChangesAsync` (after line 29):

```csharp
        await ticketRepository.SaveChangesAsync(ct);

        await assignmentRuleEngine.EvaluateAndAssignAsync(ticket.Id, ct);

        return ToDto(ticket);
```

(Replaces the existing `await ticketRepository.SaveChangesAsync(ct);` / blank line / `return ToDto(ticket);` at lines 29–31 with the three lines above.)

### 3 — Infrastructure: EF config + seed, repositories, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet`s after the Story 21 additions:

```csharp
    public DbSet<AgentSkill> AgentSkills => Set<AgentSkill>();
    public DbSet<AgentLanguage> AgentLanguages => Set<AgentLanguage>();
    public DbSet<AssignmentRule> AssignmentRules => Set<AssignmentRule>();
```

Extend the `Ticket` block (lines 87–103) with one property line:

```csharp
            entity.Property(t => t.Language).HasMaxLength(16);
```

Extend the `Team` block (lines 148–153) with a seeded "General Queue" row:

```csharp
        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("Teams");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(256);

            // Fixed-GUID seed the auto-assignment fallback (AssignmentRuleEngine.DefaultQueueTeamId)
            // targets when no rule matches — must exist so a ticket never lands on a non-existent team.
            entity.HasData(new { Id = new Guid("33333333-3333-3333-3333-333333333301"), Name = "General Queue" });
        });
```

Add new `OnModelCreating` blocks after the Story 21 blocks:

```csharp

        modelBuilder.Entity<AgentSkill>(entity =>
        {
            entity.ToTable("AgentSkills");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Skill).IsRequired().HasMaxLength(128);
            entity.HasIndex(s => new { s.AgentId, s.Skill }).IsUnique();
        });

        modelBuilder.Entity<AgentLanguage>(entity =>
        {
            entity.ToTable("AgentLanguages");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Language).IsRequired().HasMaxLength(64);
            entity.HasIndex(l => new { l.AgentId, l.Language }).IsUnique();
        });

        modelBuilder.Entity<AssignmentRule>(entity =>
        {
            entity.ToTable("AssignmentRules");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(256);
            entity.Property(r => r.Channel).HasConversion<string?>().HasMaxLength(16);
            entity.Property(r => r.Language).HasMaxLength(64);
            entity.Property(r => r.RequiredSkill).HasMaxLength(128);
            entity.HasIndex(r => r.SortOrder);
        });
```

**File: `src/SupportCrm.Infrastructure/Persistence/AgentRepository.cs`** — implement the 4 new members, added before the closing brace (after line 21):

```csharp

    public async Task<IReadOnlyList<Agent>> GetBySkillAsync(string skill, CancellationToken ct) =>
        await dbContext.Agents
            .Where(a => dbContext.AgentSkills.Any(s => s.AgentId == a.Id && s.Skill == skill))
            .ToListAsync(ct);

    public Task AddSkillAsync(Guid agentId, string skill, CancellationToken ct)
    {
        dbContext.AgentSkills.Add(new AgentSkill(agentId, skill));
        return dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetSkillsAsync(Guid agentId, CancellationToken ct) =>
        await dbContext.AgentSkills.Where(s => s.AgentId == agentId).Select(s => s.Skill).ToListAsync(ct);

    public Task AddLanguageAsync(Guid agentId, string language, CancellationToken ct)
    {
        dbContext.AgentLanguages.Add(new AgentLanguage(agentId, language));
        return dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetLanguagesAsync(Guid agentId, CancellationToken ct) =>
        await dbContext.AgentLanguages.Where(l => l.AgentId == agentId).Select(l => l.Language).ToListAsync(ct);
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/AssignmentRuleRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AssignmentRuleRepository(SupportCrmDbContext dbContext) : IAssignmentRuleRepository
{
    public async Task<IReadOnlyList<AssignmentRule>> GetActiveOrderedAsync(CancellationToken ct) =>
        await dbContext.AssignmentRules.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync(ct);

    public Task AddAsync(AssignmentRule rule, CancellationToken ct)
    {
        dbContext.AssignmentRules.Add(rule);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add after the Story 21 registrations, before `return services;`:

```csharp
        services.AddScoped<IAssignmentRuleRepository, AssignmentRuleRepository>();
        services.AddScoped<AssignmentRuleService>();
        services.AddScoped<AssignmentRuleEngine>();
```

- After creating these files, run `dotnet ef migrations add AddAssignmentRulesAndAgentSkills --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: controller additions

**File: `src/SupportCrm.Api/Controllers/AgentsController.cs`** — add after `SetSensitiveDataAccess` (after line 30):

```csharp

    [HttpPost("{id:guid}/skills")]
    public async Task<IActionResult> AddSkill(Guid id, [FromBody] AddAgentSkillRequest request, CancellationToken ct)
    {
        await agentService.AddSkillAsync(id, request.Skill, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/skills")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetSkills(Guid id, CancellationToken ct) =>
        Ok(await agentService.GetSkillsAsync(id, ct));

    [HttpPost("{id:guid}/languages")]
    public async Task<IActionResult> AddLanguage(Guid id, [FromBody] AddAgentLanguageRequest request, CancellationToken ct)
    {
        await agentService.AddLanguageAsync(id, request.Language, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/languages")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetLanguages(Guid id, CancellationToken ct) =>
        Ok(await agentService.GetLanguagesAsync(id, ct));
```

**Create file: `src/SupportCrm.Api/Controllers/AssignmentRulesController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/assignment-rules")]
public class AssignmentRulesController(AssignmentRuleService ruleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssignmentRuleDto>>> GetAll(CancellationToken ct) =>
        Ok(await ruleService.GetActiveOrderedAsync(ct));

    [HttpPost]
    public async Task<ActionResult<AssignmentRuleDto>> Create([FromBody] CreateAssignmentRuleRequest request, CancellationToken ct)
    {
        try { return await ruleService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
```

---

## Frontend Tasks

**Implemented** (`d:\Code\selfAssessment\frontend\src\app`):

- **File: `features/sla-automation/sla.model.ts`** — `AssignmentRule`, `CreateAssignmentRuleRequest`.
- **File: `features/sla-automation/sla.service.ts`** — `getAssignmentRules`/`createAssignmentRule`.
- **Create file: `features/sla-automation/assignment-rules/assignment-rules.component.{ts,html,scss}`** — ordered rule list (category/channel/language/target columns) + create form with a team-vs-skill toggle. Route: `/admin/assignment-rules`.
- **File: `features/tickets/ticket.model.ts`** — extended `Agent` with nothing new here (skills/languages are string lists, not on the DTO); no `Agent` field changes needed for this story.
- **File: `features/tickets/ticket.service.ts`** — added `getAgentSkills`/`addAgentSkill`, `getAgentLanguages`/`addAgentLanguage`.
- **File: `features/agent-dashboard/agent-admin/agent-admin.component.{ts,html}`** — extended the existing agent list with per-agent skill/language tag chips + inline add inputs, reusing the existing list+create card pattern.
- **File: `app.routes.ts`**, **`layout/app-shell/app-shell.component.ts`** — route + sidebar nav entry ("Assignment rules").

*Not built:* a dedicated auto-assignment "trace" view (why a ticket landed where it did) — the rule list plus each ticket's assignment history (Ticket Management Story 07's existing UI) covers this well enough; flagged as a possible follow-up, not required by the AC.

---

## Edge Cases & Failure Modes

- **No active rule matches a ticket's category/channel/language** — `rule` is `null`; `targetTeamId` stays `DefaultQueueTeamId` (initialized before the `if`), so the ticket is assigned to the seeded "General Queue" team, never left unassigned.
- **A skill-routed rule matches conditions but no agent currently has that skill, or none of the skilled agents is `IsAvailable`** — `PickLeastLoadedSkilledAgentAsync` returns `null`; the engine falls through to `DefaultQueueTeamId` (the `else` branch's fallback comment), not an exception.
- **Two rules would both match a ticket** — only the first by `SortOrder` (via `GetActiveOrderedAsync`'s `OrderBy(r => r.SortOrder)` + `FirstOrDefault`) is applied; later matching rules are never evaluated, by design (documented in `AssignmentRule`'s class doc comment).
- **`AssignmentRule` constructed with neither a skill nor a team target, or with both** — rejected by the constructor's `(requiredSkill is null) == (targetTeamId is null)` check (`ArgumentException` → `400` via `AssignmentRulesController.Create`'s catch).
- **`AssignmentRule` constructed with no match conditions at all** (category, channel, and language all null) — rejected separately (`ArgumentException`) so a rule can never accidentally match every ticket unconditionally.
- **Ticket creation when no `AssignmentRuleEngine` seed data exists yet** (empty `AssignmentRules` table, fresh database) — `rules` is an empty list, `FirstOrDefault` returns `null`, same "General Queue" fallback path as "no rule matches" above; the seeded `Team` row (`33333333-...-333333333301`) guarantees this never fails with a foreign-key error.
- **`AddSkillAsync` called twice with the same agent+skill** — rejected at the database level by the unique index on `(AgentId, Skill)`; the executor should surface the resulting `DbUpdateException` as a `409`/`400` at the controller if this proves to be a common caller mistake — not handled at the service layer in this story (flagged, not silently swallowed).
- **`CreateTicketRequest.Language` omitted** (defaults to `null`) — `Ticket.Language` stays `null`; language-scoped rules simply never match such tickets (`Language is null || ...` in `MatchesConditions` only short-circuits when the *rule's* `Language` is null, not the ticket's — a rule with a `Language` condition correctly never matches a ticket with no language set, since `string.Equals(rule.Language, null, ...)` is `false`).
- **Auto-assignment fails after the ticket is already created and saved** (e.g. a future skilled-agent race) — `EvaluateAndAssignAsync` runs after `TicketService.CreateAsync`'s own `SaveChangesAsync`, so a failure here does not roll back ticket creation; the ticket exists but stays unassigned. This story does not add retry/compensation logic — flagged as a follow-up, not handled here.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/AssignmentRuleTests.cs`**:
   - `Constructor_NeitherSkillNorTeam_Throws`
   - `Constructor_BothSkillAndTeam_Throws`
   - `Constructor_NoMatchConditions_Throws`
   - `MatchesConditions_TicketLanguageNull_RuleWithLanguageCondition_DoesNotMatch`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/AssignmentRuleEngineTests.cs`**:
   - `EvaluateAndAssignAsync_NoMatchingRule_AssignsToDefaultQueueTeam`
   - `EvaluateAndAssignAsync_SkillRule_PicksLeastLoadedAvailableAgent`
   - `EvaluateAndAssignAsync_SkillRule_NoAvailableSkilledAgent_FallsBackToDefaultQueue`
   - `EvaluateAndAssignAsync_AgentAssigned_SendsAgentNotification`
3. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketServiceTests.cs`** (extend Story 05's tests):
   - `CreateAsync_CallsAssignmentRuleEngine`
4. **Integration — `tests/SupportCrm.Api.Tests/Controllers/AssignmentRulesControllerTests.cs`**:
   - `Post_RuleWithBothSkillAndTeam_Returns400`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddAssignmentRulesAndAgentSkills --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.
3. **Regression:** confirm `POST /api/tickets` still succeeds end-to-end with zero rules configured (falls back to "General Queue") and with Ticket Management Story 07's manual `PUT /api/tickets/{id}/assignment` still overriding an auto-assignment afterward.

---

## Done Criteria

- [ ] Assignment rules configurable by category, channel, language, and/or required skill (`POST`/`GET /api/assignment-rules`).
- [ ] Skill-based rules pick the least-loaded available agent with the required skill.
- [ ] A ticket matching no rule (or whose matched rule has no available skilled agent) is assigned to the seeded "General Queue" team.
- [ ] Auto-assigned agents receive an immediate in-app notification (`AgentNotificationService`).
- [ ] New tickets are evaluated automatically at creation (`TicketService.CreateAsync`).
- [ ] Agent skills/languages are manageable (`POST`/`GET /api/agents/{id}/skills`, `/languages`).
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 23.**
