namespace SwarmUi.Client.Webhooks;

public enum SwarmWebhookEventType
{
    Unknown = 0,
    ServerStart,
    ServerShutdown,
    QueueStart,
    QueueEnd,
    EveryGen,
    ManualGen
}
