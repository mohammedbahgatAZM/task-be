namespace SupportCrm.Application.Tickets;

public interface ISmsSender
{
    Task<string> SendAsync(string toPhoneNumber, string body, CancellationToken ct);
}
