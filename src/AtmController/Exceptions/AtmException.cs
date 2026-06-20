namespace Atm.Exceptions;

/// <summary>
/// Base type for all exceptions raised by the ATM domain. Catching this
/// single type is enough for a caller that just wants to know "something
/// about this ATM operation went wrong"; catching the specific subtypes
/// below lets a caller react differently to each failure reason.
/// </summary>
public class AtmException : Exception
{
    public AtmException(string message) : base(message) { }

    public AtmException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when an ATM operation is requested while the session is not in
/// the right state for it — e.g. selecting an account before a PIN has
/// been validated, or inserting a card while one is already inserted.
/// </summary>
public sealed class InvalidStateException : AtmException
{
    public InvalidStateException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a withdrawal is requested for more than the account's
/// available balance.
/// </summary>
public sealed class InsufficientFundsException : AtmException
{
    public InsufficientFundsException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a deposit or withdrawal amount is not a positive,
/// whole-dollar amount (per the "only 1-dollar bills exist, no cents"
/// simplification, all amounts are <see cref="int"/> dollars, so this
/// mainly guards against zero/negative amounts).
/// </summary>
public sealed class InvalidAmountException : AtmException
{
    public InvalidAmountException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the selected account does not exist, or is not one of the
/// accounts linked to the currently inserted card.
/// </summary>
public sealed class AccountNotFoundException : AtmException
{
    public AccountNotFoundException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the maximum number of consecutive incorrect PIN attempts
/// has been reached. The card is considered retained/blocked and the
/// session is reset; the caller must start over with
/// <c>AtmController.InsertCard</c>.
/// </summary>
public sealed class CardBlockedException : AtmException
{
    public CardBlockedException(string message) : base(message) { }
}
