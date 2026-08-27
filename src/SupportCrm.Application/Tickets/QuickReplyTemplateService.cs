namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class QuickReplyTemplateService(
    IQuickReplyTemplateRepository repository,
    ITicketRepository ticketRepository,
    ICustomerRepository customerRepository,
    TimeProvider timeProvider)
{
    public async Task<QuickReplyTemplateDto> CreateAsync(CreateQuickReplyTemplateRequest request, CancellationToken ct)
    {
        var template = new QuickReplyTemplate(request.Category, request.Name, request.Body, request.CreatedBy, timeProvider.GetUtcNow());
        await repository.AddAsync(template, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task<IReadOnlyList<QuickReplyTemplateDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).OrderBy(t => t.Category).ThenBy(t => t.Name).Select(ToDto).ToList();

    public async Task<QuickReplyTemplateDto> UpdateAsync(Guid id, UpdateQuickReplyTemplateRequest request, CancellationToken ct)
    {
        var template = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Template '{id}' was not found.");
        template.Update(request.Category, request.Name, request.Body);
        await repository.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task RetireAsync(Guid id, CancellationToken ct)
    {
        var template = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Template '{id}' was not found.");
        template.Retire();
        await repository.SaveChangesAsync(ct);
    }

    public async Task<RenderedQuickReplyDto> RenderAsync(Guid templateId, Guid ticketId, CancellationToken ct)
    {
        var template = await repository.GetByIdAsync(templateId, ct) ?? throw new KeyNotFoundException($"Template '{templateId}' was not found.");
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var customer = await customerRepository.GetByIdAsync(ticket.CustomerId, ct);

        var rendered = template.Body
            .Replace("{{CustomerName}}", customer?.Name ?? ticket.RequesterName)
            .Replace("{{TicketReferenceNumber}}", ticket.ReferenceNumber)
            .Replace("{{TicketSubject}}", ticket.Subject);

        return new RenderedQuickReplyDto(rendered);
    }

    private static QuickReplyTemplateDto ToDto(QuickReplyTemplate t) => new(t.Id, t.Category, t.Name, t.Body, t.IsRetired, t.CreatedAtUtc);
}
