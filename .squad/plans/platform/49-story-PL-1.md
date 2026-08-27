# Story 49 — Arabic & English (Story: PL-1)

---

## Prerequisites

None.

---

## Story Goal

Add `PreferredLanguage` to `Agent` and `Customer`, with a setter endpoint each. Everything else in this story (dictionaries, RTL, the switcher) is frontend work — see the frontend plan.

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Entities/Agent.cs` — `SetSupervisor`/`SetKnowledgeBaseEditor`, the exact setter shape `SetPreferredLanguage` follows.
2. `src/SupportCrm.Application/Tickets/AgentService.cs`, `SetSupervisorAsync` — the exact service-method shape this story's two new setters copy.

---

## Backend Tasks

### 1 — Domain

**File: `src/SupportCrm.Domain/Entities/Agent.cs`** — add a property and setter:

```csharp
    public string PreferredLanguage { get; private set; } = "en"; // "en" | "ar"
```
```csharp
    public void SetPreferredLanguage(string language) => PreferredLanguage = language is "en" or "ar" ? language : "en";
```

**File: `src/SupportCrm.Domain/Entities/Customer.cs`** — same property + setter, identical shape.

### 2 — Application

**File: `src/SupportCrm.Application/Tickets/AgentService.cs`** — add:

```csharp
    public async Task SetPreferredLanguageAsync(Guid agentId, string language, CancellationToken ct)
    {
        var agent = await repository.GetByIdAsync(agentId, ct) ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        agent.SetPreferredLanguage(language);
        await repository.SaveChangesAsync(ct);
    }
```

**File: `src/SupportCrm.Application/Tickets/AgentTeamDtos.cs`** — add `PreferredLanguage` to `AgentDto`'s record parameters (mirroring `IsSupervisor`), and a `SetAgentLanguageRequest(string Language)` request record.

**File: `src/SupportCrm.Application/Customers/CustomerService.cs`** — add the same `SetPreferredLanguageAsync`, and add `PreferredLanguage` to `CustomerDto`.

**File: `src/SupportCrm.Application/Customers/CustomerDtos.cs`** — add `SetCustomerLanguageRequest(string Language)`.

### 3 — Infrastructure

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — in the existing `Agent`/`Customer` `OnModelCreating` blocks, add:

```csharp
            entity.Property(a => a.PreferredLanguage).IsRequired().HasMaxLength(4);
```

(same line, `c =>` instead of `a =>`, in the `Customer` block).

### 4 — Api

**File: `src/SupportCrm.Api/Controllers/AgentsController.cs`** — add:

```csharp

    [HttpPut("{id:guid}/language")]
    public async Task<IActionResult> SetLanguage(Guid id, [FromBody] SetAgentLanguageRequest request, CancellationToken ct)
    {
        try { await agentService.SetPreferredLanguageAsync(id, request.Language, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
```

**File: `src/SupportCrm.Api/Controllers/CustomersController.cs`** — add the equivalent `PUT {id:guid}/language` action calling `customerService.SetPreferredLanguageAsync`.

---

## Edge Cases & Failure Modes

- **An unrecognized language code** (`"fr"`, empty string) — `SetPreferredLanguage` silently falls back to `"en"` rather than throwing — a display preference is never worth a hard failure.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx`.
2. **Migration:** covered by this feature's single consolidated `AddPlatform` migration (see Story 52's Verification Steps).

---

## Done Criteria

- [ ] `Agent`/`Customer` each store and expose a `PreferredLanguage`, settable via their own endpoint.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
