namespace Atm.Domain;

/// <summary>
/// An immutable record of a completed ATM operation (deposit or
/// withdrawal). Returned to callers as a receipt, and kept in the ATM's
/// own transaction log for auditing — independent of whatever
/// record-keeping the bank itself performs.
/// </summary>
public sealed record Transaction
{
    public Guid TransactionId { get; }
    public string AccountId { get; }
    public TransactionType Type { get; }
    public int Amount { get; }
    public TransactionStatus Status { get; }
    public DateTimeOffset Timestamp { get; }

    public Transaction(string accountId, TransactionType type, int amount, TransactionStatus status)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new ArgumentException("Account id must not be null or blank.", nameof(accountId));
        }

        TransactionId = Guid.NewGuid();
        AccountId = accountId;
        Type = type;
        Amount = amount;
        Status = status;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
