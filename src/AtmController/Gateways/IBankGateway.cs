using Atm.Domain;

namespace Atm.Gateways;

/// <summary>
/// The boundary between the ATM and a bank's systems.
/// </summary>
/// <remarks>
/// This is the single seam that a future integration with a real bank
/// (over a REST/gRPC API, for example) would implement. Everything above
/// this interface — <see cref="Atm.Services.AtmService"/> and
/// <see cref="Atm.Controllers.AtmController"/> — depends only on this
/// abstraction and is completely unaware of how/where account data is
/// actually stored or validated.
/// <para>
/// <b>Important security constraint:</b> a real bank would never hand the
/// actual PIN to the ATM. <see cref="ValidatePin"/> therefore returns a
/// simple yes/no answer rather than the PIN itself, and there is no
/// member anywhere on this interface (or used by anything in this
/// project) that retrieves a PIN value.
/// </para>
/// </remarks>
public interface IBankGateway
{
    /// <summary>
    /// Asks the bank whether <paramref name="pin"/> is the correct PIN
    /// for the given card. The PIN itself is never exposed by this call —
    /// only a boolean verdict.
    /// </summary>
    bool ValidatePin(string cardNumber, string pin);

    /// <summary>
    /// Returns the accounts linked to the given card.
    /// </summary>
    IReadOnlyList<Account> GetAccounts(string cardNumber);

    /// <summary>
    /// Returns the current balance of the given account.
    /// </summary>
    int GetBalance(string accountId);

    /// <summary>
    /// Credits <paramref name="amount"/> to the given account.
    /// </summary>
    void Deposit(string accountId, int amount);

    /// <summary>
    /// Debits <paramref name="amount"/> from the given account.
    /// Implementations are expected to reject a withdrawal that would
    /// overdraw the account.
    /// </summary>
    void Withdraw(string accountId, int amount);
}
