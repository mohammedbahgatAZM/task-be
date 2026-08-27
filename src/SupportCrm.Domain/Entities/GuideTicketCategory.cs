namespace SupportCrm.Domain.Entities;

public class GuideTicketCategory
{
    public Guid Id { get; private set; }
    public Guid GuideId { get; private set; }
    public Guid TicketCategoryId { get; private set; }

    private GuideTicketCategory() { } // EF Core

    public GuideTicketCategory(Guid guideId, Guid ticketCategoryId)
    {
        Id = Guid.NewGuid();
        GuideId = guideId;
        TicketCategoryId = ticketCategoryId;
    }
}
