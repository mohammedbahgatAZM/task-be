namespace SupportCrm.Application.Tickets;

using SupportCrm.Application.Customers;

/// <summary>
/// Resolves a ticket requester (name + optional contact value) to a Customer id,
/// reusing Customer Management's contact-detail lookup and duplicate-detection
/// rather than re-implementing matching here. This is the concrete fix for the
/// assumption Customer Management's CM-1 flagged: "opening a ticket links to the
/// correct existing customer profile (no duplicates)".
/// </summary>
public class TicketCustomerResolver(
    IContactDetailRepository contactDetailRepository,
    CustomerService customerService)
{
    private const double StrongNameMatchThreshold = 0.7;

    public async Task<Guid> ResolveCustomerIdAsync(string requesterName, string? requesterContactValue, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requesterContactValue))
        {
            var existingContact = await contactDetailRepository.FindByValueAsync(requesterContactValue, ct);
            if (existingContact is not null)
                return existingContact.CustomerId;
        }

        var candidates = await customerService.FindDuplicatesAsync(requesterName, null, ct);
        var strongMatch = candidates.FirstOrDefault(c => c.Score >= StrongNameMatchThreshold);
        if (strongMatch is not null)
            return strongMatch.Customer.Id;

        var created = await customerService.CreateAsync(new CreateCustomerRequest(requesterName, null, null), ct);
        return created.Id;
    }
}
