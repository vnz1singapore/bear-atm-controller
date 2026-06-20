using Atm.Domain;
using Atm.Exceptions;
using Atm.Gateways;

namespace Atm.Services;

/// <summary>
/// Holds the ATM-side business rules for the supported operations
/// (balance inquiry, deposit, withdrawal, PIN check, account lookup) and
/// mediates all access to the <see cref="IBankGateway"/>.
/// </summary>
/// <remarks>
/// This class deliberately knows nothing about ATM session state (which
/// card is inserted, whether a PIN has been entered, etc.) — that is
/// <see cref="Atm.Controllers.AtmController"/>'s job. Keeping the two
/// separated means the business rules here can be unit tested without
/// needing to drive a full insert-card/enter-pin/select-account session,
/// and the controller's state machine can be tested independently of how
/// the rules are implemented.
/// </remarks>
public class AtmService
{
    private readonly IBankGateway _bankGateway;
    private readonly List<Transaction> _transactionLog = new();

    public AtmService(IBankGateway bankGateway)
    {
        _bankGateway = bankGateway ?? throw new ArgumentNullException(nameof(bankGateway));
    }

    /// <summary>
    /// Asks the bank whether <paramref name="pin"/> is correct for
    /// <paramref name="cardNumber"/>. Never has access to (and never
    /// returns) the actual PIN value.
    /// </summary>
    public bool ValidatePin(string cardNumber, string pin) => _bankGateway.ValidatePin(cardNumber, pin);

    public IReadOnlyList<Account> GetAccounts(string cardNumber) => _bankGateway.GetAccounts(cardNumber);

    public int CheckBalance(string accountId) => _bankGateway.GetBalance(accountId);

    public Transaction Deposit(string accountId, int amount)
    {
        RequirePositiveAmount(amount);

        _bankGateway.Deposit(accountId, amount);

        var transaction = new Transaction(accountId, TransactionType.Deposit, amount, TransactionStatus.Success);
        _transactionLog.Add(transaction);
        return transaction;
    }

    public Transaction Withdraw(string accountId, int amount)
    {
        RequirePositiveAmount(amount);

        var currentBalance = _bankGateway.GetBalance(accountId);
        if (currentBalance < amount)
        {
            throw new InsufficientFundsException(
                $"Insufficient funds: balance is {currentBalance}, requested {amount}.");
        }

        _bankGateway.Withdraw(accountId, amount);

        var transaction = new Transaction(accountId, TransactionType.Withdrawal, amount, TransactionStatus.Success);
        _transactionLog.Add(transaction);
        return transaction;
    }

    /// <summary>
    /// Every successful transaction made through this service instance,
    /// in chronological order. Useful for receipts/auditing.
    /// </summary>
    public IReadOnlyList<Transaction> GetTransactionHistory() => _transactionLog.AsReadOnly();

    private static void RequirePositiveAmount(int amount)
    {
        if (amount <= 0)
        {
            throw new InvalidAmountException($"Amount must be a positive whole dollar value, got: {amount}.");
        }
    }
}
