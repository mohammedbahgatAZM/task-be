namespace SupportCrm.Domain.Entities;

public class WebFormFieldDefinition
{
    public Guid Id { get; private set; }
    public Guid CategoryId { get; private set; }
    public string FieldName { get; private set; } = default!;
    public WebFormFieldType FieldType { get; private set; }
    public bool IsRequired { get; private set; }
    public int DisplayOrder { get; private set; }

    private WebFormFieldDefinition() { } // EF Core

    public WebFormFieldDefinition(Guid categoryId, string fieldName, WebFormFieldType fieldType, bool isRequired, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name is required.", nameof(fieldName));

        Id = Guid.NewGuid();
        CategoryId = categoryId;
        FieldName = fieldName;
        FieldType = fieldType;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
    }
}
