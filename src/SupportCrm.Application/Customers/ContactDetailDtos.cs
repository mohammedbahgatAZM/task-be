namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;

public record AddContactDetailRequest(ContactChannelType ChannelType, string Value, bool IsPrimary, string ChangedBy);
public record UpdateContactDetailRequest(string Value, string ChangedBy);
public record SetPrimaryContactDetailRequest(string ChangedBy);
public record SetPreferredChannelRequest(ContactChannelType? Channel, string ChangedBy);
public record SetAddressRequest(string? Address, string ChangedBy);

public record ContactDetailDto(Guid Id, ContactChannelType ChannelType, string Value, bool IsPrimary, DateTimeOffset CreatedAtUtc);

public record ContactDetailChangeLogDto(Guid Id, string ChangeType, string? OldValue, string? NewValue, string ChangedBy, DateTimeOffset ChangedAtUtc);
