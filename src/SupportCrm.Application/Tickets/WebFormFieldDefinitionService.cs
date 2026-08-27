namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class WebFormFieldDefinitionService(IWebFormFieldDefinitionRepository repository)
{
    public async Task<WebFormFieldDefinitionDto> CreateAsync(CreateWebFormFieldDefinitionRequest request, CancellationToken ct)
    {
        var definition = new WebFormFieldDefinition(request.CategoryId, request.FieldName.Trim(), request.FieldType, request.IsRequired, request.DisplayOrder);
        await repository.AddAsync(definition, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(definition);
    }

    public async Task<IReadOnlyList<WebFormFieldDefinitionDto>> GetByCategoryAsync(Guid categoryId, CancellationToken ct) =>
        (await repository.GetByCategoryAsync(categoryId, ct)).OrderBy(d => d.DisplayOrder).Select(ToDto).ToList();

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var definition = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Field definition '{id}' was not found.");
        await repository.DeleteAsync(definition, ct);
        await repository.SaveChangesAsync(ct);
    }

    private static WebFormFieldDefinitionDto ToDto(WebFormFieldDefinition d) => new(d.Id, d.CategoryId, d.FieldName, d.FieldType, d.IsRequired, d.DisplayOrder);
}
