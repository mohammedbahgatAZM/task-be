namespace SupportCrm.Domain.Entities;

// INT-3/INT-4 — the fixed set of connector "kinds" this prototype's configurable connector
// framework understands. Email/Sms/WhatsApp back the already-shipped Communication Channels
// mock senders (CC-1/CC-2/CC-4); Erp/Billing/Inventory are the "external systems" of INT-2/INT-4,
// each with a mock connector standing in for a real integration (no real provider account exists
// in this codebase — same documented scope decision as Communication Channels).
public enum IntegrationConnectorType
{
    Email,
    Sms,
    WhatsApp,
    Erp,
    Billing,
    Inventory
}
