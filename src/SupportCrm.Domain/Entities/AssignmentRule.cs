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
