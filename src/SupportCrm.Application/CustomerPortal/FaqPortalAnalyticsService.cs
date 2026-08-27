namespace SupportCrm.Application.CustomerPortal;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

// "Deflection rate" is an honest proxy, not a causal measurement: the share of impressions
// where the customer did NOT go on to submit a ticket in that same draft session. See this
// story's intake for why a stronger claim isn't achievable without real session/auth infra.
public class FaqPortalAnalyticsService(
    IFaqRepository faqRepository,
    IFaqPortalImpressionRepository impressionRepository,
    TimeProvider timeProvider)
{
    public async Task LogImpressionAsync(Guid faqId, string draftSessionId, CancellationToken ct)
    {
        _ = await faqRepository.GetByIdAsync(faqId, ct) ?? throw new KeyNotFoundException($"FAQ '{faqId}' was not found.");
        await impressionRepository.AddAsync(new FaqPortalImpression(faqId, draftSessionId, timeProvider.GetUtcNow()), ct);
        await impressionRepository.SaveChangesAsync(ct);
    }

    public async Task MarkSessionConvertedAsync(string draftSessionId, CancellationToken ct)
    {
        var impressions = await impressionRepository.GetBySessionAsync(draftSessionId, ct);
        foreach (var impression in impressions)
            impression.MarkLedToTicketSubmission();
        await impressionRepository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FaqDeflectionReportItemDto>> GetDeflectionReportAsync(CancellationToken ct)
    {
        var impressions = await impressionRepository.GetAllAsync(ct);
        return impressions
            .GroupBy(i => i.FaqId)
            .Select(g => new FaqDeflectionReportItemDto(
                g.Key,
                g.Count(),
                g.Count(i => i.LedToTicketSubmission),
                Math.Round(100.0 * g.Count(i => !i.LedToTicketSubmission) / g.Count(), 1)))
            .OrderByDescending(r => r.DeflectionRatePercentage)
            .ToList();
    }
}
