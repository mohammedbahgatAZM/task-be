namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class GuideService(IGuideRepository repository, IAgentRepository agentRepository, TimeProvider timeProvider)
{
    public async Task<GuideDto> CreateAsync(CreateGuideRequest request, CancellationToken ct)
    {
        await RequireEditorAsync(request.EditorAgentId, ct);
        var guide = new Guide(request.TitleEn?.Trim(), request.TitleAr?.Trim(), request.BodyEn?.Trim(), request.BodyAr?.Trim(),
            request.VideoUrl?.Trim(), request.AuthorName.Trim(), timeProvider.GetUtcNow());
        await repository.AddAsync(guide, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(guide);
    }

    public async Task<GuideDto> GetByIdAsync(Guid id, CancellationToken ct) =>
        ToDto(await repository.GetByIdAsync(id, ct) ?? throw new GuideNotFoundException(id.ToString()));

    public async Task<IReadOnlyList<GuideDto>> GetAllAsync(bool includeUnpublished, CancellationToken ct) =>
        (await repository.GetAllAsync(includeUnpublished, ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<GuideDto>> GetByTicketCategoryAsync(Guid ticketCategoryId, bool includeUnpublished, CancellationToken ct) =>
        (await repository.GetByTicketCategoryAsync(ticketCategoryId, includeUnpublished, ct)).Select(ToDto).ToList();

    public async Task<GuideDto> UpdateAsync(Guid id, UpdateGuideRequest request, CancellationToken ct)
    {
        await RequireEditorAsync(request.EditorAgentId, ct);
        var guide = await repository.GetByIdAsync(id, ct) ?? throw new GuideNotFoundException(id.ToString());
        guide.RecordUpdate(request.TitleEn?.Trim(), request.TitleAr?.Trim(), request.BodyEn?.Trim(), request.BodyAr?.Trim(), request.VideoUrl?.Trim(), request.ChangedBy, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(ct);
        return ToDto(guide);
    }

    // Flagging outdated is intentionally NOT editor-gated — any agent can raise the concern;
    // only an editor can act on it (via Story 29's workflow or a future un-flag/publish action).
    public async Task FlagOutdatedAsync(Guid id, FlagGuideOutdatedRequest request, CancellationToken ct)
    {
        var guide = await repository.GetByIdAsync(id, ct) ?? throw new GuideNotFoundException(id.ToString());
        guide.FlagOutdated(request.Reason, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(ct);
    }

    public async Task LinkCategoryAsync(Guid guideId, Guid ticketCategoryId, Guid editorAgentId, CancellationToken ct)
    {
        await RequireEditorAsync(editorAgentId, ct);
        _ = await repository.GetByIdAsync(guideId, ct) ?? throw new GuideNotFoundException(guideId.ToString());
        await repository.AddCategoryLinkAsync(new GuideTicketCategory(guideId, ticketCategoryId), ct);
        await repository.SaveChangesAsync(ct);
    }

    public async Task UnlinkCategoryAsync(Guid guideId, Guid ticketCategoryId, Guid editorAgentId, CancellationToken ct)
    {
        await RequireEditorAsync(editorAgentId, ct);
        await repository.RemoveCategoryLinkAsync(guideId, ticketCategoryId, ct);
        await repository.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<Guid>> GetLinkedCategoriesAsync(Guid guideId, CancellationToken ct) =>
        repository.GetLinkedTicketCategoryIdsAsync(guideId, ct);

    internal async Task RequireEditorAsync(Guid agentId, CancellationToken ct)
    {
        var agent = await agentRepository.GetByIdAsync(agentId, ct);
        if (agent is null || !agent.IsKnowledgeBaseEditor)
            throw new KbEditorRequiredException(agentId);
    }

    internal static GuideDto ToDto(Guide g) => new(g.Id, g.TitleEn, g.TitleAr, g.BodyEn, g.BodyAr, g.VideoUrl, g.Status, g.AuthorName, g.LastUpdatedByName, g.LastUpdatedAtUtc, g.IsFlaggedOutdated, g.FlaggedReason);
}
