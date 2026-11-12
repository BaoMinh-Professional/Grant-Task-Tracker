namespace GrantTracker.Core.Idempotency;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord() { }
    public IdempotencyRecord(string key, string operation, string requestHash, string resultJson, DateTime createdAtUtc)
    {
        Key = key; Operation = operation; RequestHash = requestHash; ResultJson = resultJson; CreatedAtUtc = createdAtUtc;
    }
    public string Key { get; private set; } = "";
    public string Operation { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string ResultJson { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }

    // A reservation holds an empty result until the command succeeds. It is only committed with that result.
    public void SetResult(string resultJson) => ResultJson = resultJson;
}
