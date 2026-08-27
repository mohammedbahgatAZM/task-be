namespace SupportCrm.Domain.Entities;

public class ChatSession
{
    public Guid Id { get; private set; }
    public string CustomerName { get; private set; } = default!;
    public string? CustomerContactValue { get; private set; }
    public ChatSessionStatus Status { get; private set; }
    public ChatSessionMode Mode { get; private set; } = ChatSessionMode.Human;
    public Guid? AssignedAgentId { get; private set; }
    public Guid? ResultingTicketId { get; private set; }
    public bool CustomerIsTyping { get; private set; }
    public bool AgentIsTyping { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }

    private ChatSession() { } // EF Core

    public ChatSession(string customerName, string? customerContactValue, DateTimeOffset startedAtUtc, ChatSessionMode mode = ChatSessionMode.Human)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name is required.", nameof(customerName));

        Id = Guid.NewGuid();
        CustomerName = customerName;
        CustomerContactValue = customerContactValue;
        Mode = mode;
        // Bot sessions are immediately "active" (the bot is always available); human sessions
        // keep the existing Queued-until-assigned behavior, unchanged for CC-3's call site.
        Status = mode == ChatSessionMode.Bot ? ChatSessionStatus.Active : ChatSessionStatus.Queued;
        StartedAtUtc = startedAtUtc;
    }

    public void AssignAgent(Guid agentId)
    {
        AssignedAgentId = agentId;
        Status = ChatSessionStatus.Active;
    }

    public void Complete(Guid resultingTicketId, DateTimeOffset atUtc)
    {
        Status = ChatSessionStatus.Completed;
        ResultingTicketId = resultingTicketId;
        EndedAtUtc = atUtc;
    }

    public void SetTyping(bool isCustomer, bool isTyping)
    {
        if (isCustomer) CustomerIsTyping = isTyping;
        else AgentIsTyping = isTyping;
    }

    public void RequestHuman(Guid? assignedAgentId)
    {
        Mode = ChatSessionMode.Human;
        if (assignedAgentId is not null)
        {
            AssignedAgentId = assignedAgentId;
            Status = ChatSessionStatus.Active;
        }
        else
        {
            Status = ChatSessionStatus.Queued;
        }
    }
}
