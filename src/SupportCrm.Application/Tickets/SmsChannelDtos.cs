namespace SupportCrm.Application.Tickets;

public record SendSmsRequest(string Body, string ChangedBy);
public record RecordSmsDeliveryFailureRequest(Guid TicketMessageId, string Reason);
