namespace SupportCrm.Domain.Entities;

// Distinct from TicketCategory (Ticket Management TM-2) — this taxonomy organizes knowledge
// content (FAQs/Articles/Guides), TicketCategory organizes ticket routing/reporting.
public class KbCategory
{
    public Guid Id { get; private set; }
    public string? NameEn { get; private set; }
    public string? NameAr { get; private set; }
    public bool IsActive { get; private set; } = true;

    private KbCategory() { } // EF Core

    public KbCategory(string? nameEn, string? nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameEn) && string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("At least one of NameEn/NameAr is required.", nameof(nameEn));
        Id = Guid.NewGuid();
        NameEn = nameEn;
        NameAr = nameAr;
    }

    public void Deactivate() => IsActive = false;
}
