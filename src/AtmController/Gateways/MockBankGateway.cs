using Atm.Domain;
using Atm.Exceptions;

namespace Atm.Gateways;

/// <summary>
/// A simple in-memory <see cref="IBankGateway"/> implementation, used in
/// place of a real bank integration today.
/// </summary>
/// <remarks>
/// This is the only class in the project that is allowed to know a
/// card's PIN value — and even here it is only ever compared against,
/// never returned to a caller. <see cref="ValidatePin"/> is the sole way
/// to query it, and it returns a <see cref="bool"/>.
/// <para>
/// This class is also a natural seam for swapping in a real
/// implementation later (e.g. <c>RestBankGateway</c>,
/// <c>GrpcBankGateway</c>) without touching any other class in the
/// project, since both would implement the same <see cref="IBankGateway"/>
/// interface.
/// </para>
/// </remarks>
public class MockBankGateway : IBankGateway
{
    private readonly Dictionary<string, string> _cardPins = new();
    private readonly Dictionary<string, List<string>> _cardToAccountIds = new();
    private readonly Dictionary<string, int> _accountBalances = new();

    /// <summary>
    /// Test/setup helper: registers a card with its PIN and the list of
    /// account ids it has access to. Stands in for whatever
    /// account-opening process a real bank would have.
    /// </summary>
    public void RegisterCard(string cardNumber, string pin, IEnumerable<string> accountIds)
    {
        _cardPins[cardNumber] = pin;
        _cardToAccountIds[cardNumber] = new List<string>(accountIds);
    }

    /// <summary>
    /// Test/setup helper: opens an account with the given starting
    /// balance.
    /// </summary>
    public void RegisterAccount(string accountId, int initialBalance)
    {
        if (initialBalance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialBalance), "Initial balance must not be negative.");
        }

        _accountBalances[accountId] = initialBalance;
    }

    public bool ValidatePin(string cardNumber, string pin) =>
        _cardPins.TryGetValue(cardNumber, out var actualPin) && actualPin == pin;

    public IReadOnlyList<Account> GetAccounts(string cardNumber)
    {
        if (!_cardToAccountIds.TryGetValue(cardNumber, out var accountIds))
        {
            return Array.Empty<Account>();
        }

        return accountIds.Select(id => new Account(id, GetBalance(id))).ToList();
    }

    public int GetBalance(string accountId)
    {
        if (!_accountBalances.TryGetValue(accountId, out var balance))
        {
            throw new AccountNotFoundException($"No such account: {accountId}");
        }

        return balance;
    }

    public void Deposit(string accountId, int amount)
    {
        var currentBalance = GetBalance(accountId);
        _accountBalances[accountId] = currentBalance + amount;
    }

    public void Withdraw(string accountId, int amount)
    {
        var currentBalance = GetBalance(accountId);
        if (currentBalance < amount)
        {
            throw new InsufficientFundsException(
                $"Account {accountId} has insufficient funds for withdrawal of {amount}.");
        }

        _accountBalances[accountId] = currentBalance - amount;
    }
}
