namespace SupportCrm.Domain.Entities;

public class Ticket
{
    public Guid Id { get; private set; }
    public string ReferenceNumber { get; private set; } = default!;
    public Guid CustomerId { get; private set; }
    public TicketChannel Channel { get; private set; }
    public string Subject { get; private set; } = default!;
    public string? Description { get; private set; }
    public TicketStatus Status { get; private set; }
    public string RequesterName { get; private set; } = default!;
    public string? RequesterContactValue { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public Guid? CategoryId { get; private set; }
    public TicketPriority Priority { get; private set; } = TicketPriority.Medium;
    public Guid? AssignedAgentId { get; private set; }
    public Guid? AssignedTeamId { get; private set; }
    public DateTimeOffset? LastEscalatedAtUtc { get; private set; }
    public string? Language { get; private set; }
    public Guid? DepartmentId { get; private set; }

    private Ticket() { } // EF Core

    public Ticket(string referenceNumber, Guid customerId, TicketChannel channel, string subject, string? description,
        string requesterName, string? requesterContactValue, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
            throw new ArgumentException("Reference number is required.", nameof(referenceNumber));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(requesterName))
            throw new ArgumentException("Requester name is required.", nameof(requesterName));

        Id = Guid.NewGuid();
        ReferenceNumber = referenceNumber;
        CustomerId = customerId;
        Channel = channel;
        Subject = subject;
        Description = description;
        Status = TicketStatus.New;
        RequesterName = requesterName;
        RequesterContactValue = requesterContactValue;
        CreatedAtUtc = createdAtUtc;
    }

    public void SetStatus(TicketStatus status, DateTimeOffset atUtc)
    {
        Status = status;
        ClosedAtUtc = status is TicketStatus.Closed ? atUtc : null;
    }

    public void SetCategory(Guid? categoryId) => CategoryId = categoryId;

    public void SetPriority(TicketPriority priority) => Priority = priority;

    public void AssignTo(Guid? agentId, Guid? teamId)
    {
        if (agentId is not null && teamId is not null)
            throw new InvalidOperationException("A ticket can be assigned to an agent or a team, not both.");
        AssignedAgentId = agentId;
        AssignedTeamId = teamId;
    }

    public void MarkEscalated(DateTimeOffset atUtc) => LastEscalatedAtUtc = atUtc;

    public void SetLanguage(string? language) => Language = language;

    public void SetDepartment(Guid? departmentId) => DepartmentId = departmentId;
}
