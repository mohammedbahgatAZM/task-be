namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Repositories;

public class CustomerProfileService(ICustomerRepository customerRepository)
{
    public async Task SetPreferredChannelAsync(Guid customerId, SetPreferredChannelRequest request, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        customer.SetPreferredContactChannel(request.Channel);
        await customerRepository.SaveChangesAsync(ct);
    }

    public async Task SetAddressAsync(Guid customerId, SetAddressRequest request, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        customer.SetAddress(request.Address);
        await customerRepository.SaveChangesAsync(ct);
    }
}
