namespace Atm.Domain;

/// <summary>
/// Immutable snapshot of a bank account, as seen by the ATM.
/// </summary>
/// <remarks>
/// The ATM never owns the source of truth for a balance — it is always a
/// read-only snapshot obtained from <see cref="Atm.Gateways.IBankGateway"/>.
/// Mutating an account (deposit/withdraw) always goes back through the
/// gateway; this object is never mutated in place.
/// </remarks>
public sealed record Account
{
    public string AccountId { get; }
    public int Balance { get; }

    public Account(string accountId, int balance)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            throw new ArgumentException("Account id must not be null or blank.", nameof(accountId));
        }

        if (balance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balance), "Balance must not be negative.");
        }

        AccountId = accountId;
        Balance = balance;
    }
}
