namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class WebFormSubmissionService(
    IWebFormFieldDefinitionRepository fieldDefinitionRepository,
    TicketIngestionService ingestionService)
{
    public async Task<WebFormSubmissionResultDto> SubmitAsync(SubmitWebFormRequest request, CancellationToken ct)
    {
        var definitions = await fieldDefinitionRepository.GetByCategoryAsync(request.CategoryId, ct);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.RequesterName))
            errors.Add("Name is required.");
        if (string.IsNullOrWhiteSpace(request.RequesterContactValue))
            errors.Add("Contact value is required.");

        foreach (var field in definitions.Where(d => d.IsRequired))
        {
            if (!request.FieldValues.TryGetValue(field.FieldName, out var value) || string.IsNullOrWhiteSpace(value))
                errors.Add($"'{field.FieldName}' is required.");
        }

        if (errors.Count > 0)
            throw new WebFormValidationException(errors);

        var description = string.Join("\n", request.FieldValues.Select(kv => $"{kv.Key}: {kv.Value}"));
        var ticket = await ingestionService.IngestInboundMessageAsync(
            new IngestInboundMessageRequest(TicketChannel.WebForm, request.RequesterName, request.RequesterContactValue, "Web form submission", description), ct);

        return new WebFormSubmissionResultDto(ticket.Id, ticket.ReferenceNumber);
    }
}
