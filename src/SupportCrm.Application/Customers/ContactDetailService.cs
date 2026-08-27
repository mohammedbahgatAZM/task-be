namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ContactDetailService(
    ICustomerRepository customerRepository,
    IContactDetailRepository contactDetailRepository,
    TimeProvider timeProvider)
{
    public async Task<ContactDetailDto> AddAsync(Guid customerId, AddContactDetailRequest request, CancellationToken ct)
    {
        _ = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);

        var validationError = ContactDetailValidation.Validate(request.ChannelType, request.Value);
        if (validationError is not null)
            throw new ArgumentException(validationError, nameof(request));

        var existing = await contactDetailRepository.GetByCustomerAsync(customerId, ct);
        var makePrimary = request.IsPrimary || !existing.Any(c => c.ChannelType == request.ChannelType);

        if (makePrimary)
            foreach (var other in existing.Where(c => c.ChannelType == request.ChannelType && c.IsPrimary))
                other.SetPrimary(false);

        var now = timeProvider.GetUtcNow();
        var contactDetail = new ContactDetail(customerId, request.ChannelType, request.Value.Trim(), makePrimary, now);
        await contactDetailRepository.AddAsync(contactDetail, ct);
        await contactDetailRepository.AddChangeLogAsync(
            new ContactDetailChangeLogEntry(contactDetail.Id, customerId, "Created", null, contactDetail.Value, request.ChangedBy, now), ct);
        await contactDetailRepository.SaveChangesAsync(ct);

        return ToDto(contactDetail);
    }

    public async Task<ContactDetailDto> UpdateValueAsync(Guid contactDetailId, UpdateContactDetailRequest request, CancellationToken ct)
    {
        var contactDetail = await contactDetailRepository.GetByIdAsync(contactDetailId, ct)
            ?? throw new KeyNotFoundException($"Contact detail '{contactDetailId}' was not found.");

        var validationError = ContactDetailValidation.Validate(contactDetail.ChannelType, request.Value);
        if (validationError is not null)
            throw new ArgumentException(validationError, nameof(request));

        var oldValue = contactDetail.Value;
        contactDetail.UpdateValue(request.Value.Trim());

        await contactDetailRepository.AddChangeLogAsync(
            new ContactDetailChangeLogEntry(contactDetail.Id, contactDetail.CustomerId, "ValueChanged", oldValue, contactDetail.Value, request.ChangedBy, timeProvider.GetUtcNow()), ct);
        await contactDetailRepository.SaveChangesAsync(ct);

        return ToDto(contactDetail);
    }

    public async Task SetPrimaryAsync(Guid contactDetailId, SetPrimaryContactDetailRequest request, CancellationToken ct)
    {
        var contactDetail = await contactDetailRepository.GetByIdAsync(contactDetailId, ct)
            ?? throw new KeyNotFoundException($"Contact detail '{contactDetailId}' was not found.");

        var siblings = await contactDetailRepository.GetByCustomerAsync(contactDetail.CustomerId, ct);
        foreach (var other in siblings.Where(c => c.ChannelType == contactDetail.ChannelType && c.Id != contactDetail.Id && c.IsPrimary))
            other.SetPrimary(false);

        contactDetail.SetPrimary(true);
        await contactDetailRepository.AddChangeLogAsync(
            new ContactDetailChangeLogEntry(contactDetail.Id, contactDetail.CustomerId, "PrimaryChanged", "false", "true", request.ChangedBy, timeProvider.GetUtcNow()), ct);
        await contactDetailRepository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ContactDetailDto>> GetForCustomerAsync(Guid customerId, CancellationToken ct) =>
        (await contactDetailRepository.GetByCustomerAsync(customerId, ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ContactDetailChangeLogDto>> GetChangeLogAsync(Guid customerId, CancellationToken ct) =>
        (await contactDetailRepository.GetChangeLogAsync(customerId, ct))
            .OrderByDescending(e => e.ChangedAtUtc)
            .Select(e => new ContactDetailChangeLogDto(e.Id, e.ChangeType, e.OldValue, e.NewValue, e.ChangedBy, e.ChangedAtUtc))
            .ToList();

    private static ContactDetailDto ToDto(ContactDetail c) => new(c.Id, c.ChannelType, c.Value, c.IsPrimary, c.CreatedAtUtc);
}
