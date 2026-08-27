namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record CreateWebFormFieldDefinitionRequest(Guid CategoryId, string FieldName, WebFormFieldType FieldType, bool IsRequired, int DisplayOrder);
public record WebFormFieldDefinitionDto(Guid Id, Guid CategoryId, string FieldName, WebFormFieldType FieldType, bool IsRequired, int DisplayOrder);

public record SubmitWebFormRequest(Guid CategoryId, string RequesterName, string RequesterContactValue, Dictionary<string, string> FieldValues);
public record WebFormSubmissionResultDto(Guid TicketId, string TicketReferenceNumber);

public class WebFormValidationException(IReadOnlyList<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
