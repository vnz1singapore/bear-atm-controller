using Atm.Domain;
using Atm.Exceptions;
using Atm.Services;

namespace Atm.Controllers;

/// <summary>
/// Entry point for driving an ATM session.
/// </summary>
/// <remarks>
/// <para>
/// This class owns exactly one thing: the session state machine that
/// enforces the required flow —
/// <c>InsertCard -&gt; EnterPin -&gt; SelectAccount -&gt; (CheckBalance | Deposit | Withdraw)</c>.
/// </para>
/// <para>
/// It deliberately contains no business rules (how a PIN is checked,
/// what counts as sufficient funds, etc.) — those live in
/// <see cref="AtmService"/>. It also contains no I/O, hardware, or
/// networking code: callers are expected to be a UI layer (console, GUI,
/// or, in the future, real card-reader/cash-dispenser hardware drivers)
/// that this controller knows nothing about. That separation is what
/// lets the same controller and service be reused unmodified once those
/// things exist.
/// </para>
/// <para>
/// A single <see cref="AtmController"/> instance represents a single
/// physical ATM and is not thread-safe by design — much like a real ATM
/// only serves one customer session at a time.
/// </para>
/// </remarks>
public class AtmController
{
    /// <summary>
    /// Number of consecutive incorrect PIN attempts allowed before the
    /// card is retained.
    /// </summary>
    public const int MaxPinAttempts = 3;

    private enum SessionState
    {
        NoCard,
        CardInserted,
        Authenticated,
        AccountSelected
    }

    private readonly AtmService _atmService;

    private SessionState _state = SessionState.NoCard;
    private Card? _currentCard;
    private string? _currentAccountId;
    private int _failedPinAttempts;

    public AtmController(AtmService atmService)
    {
        _atmService = atmService ?? throw new ArgumentNullException(nameof(atmService));
    }

    /// <summary>
    /// Begins a new session with the given card. Must be the first call
    /// in any session.
    /// </summary>
    public void InsertCard(Card card)
    {
        if (_state != SessionState.NoCard)
        {
            throw new InvalidStateException("A card is already inserted; eject it before inserting another.");
        }

        _currentCard = card ?? throw new ArgumentNullException(nameof(card));
        _failedPinAttempts = 0;
        _state = SessionState.CardInserted;
    }

    /// <summary>
    /// Submits a PIN for the currently inserted card.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the PIN was correct (the session is now
    /// authenticated); <c>false</c> if it was incorrect (the session
    /// remains at the "card inserted" step so the caller may retry).
    /// </returns>
    /// <exception cref="CardBlockedException">
    /// Thrown if this incorrect attempt was the <see cref="MaxPinAttempts"/>th
    /// in a row; the session is reset as if the card had been
    /// ejected/retained.
    /// </exception>
    public bool EnterPin(string pin)
    {
        RequireState(SessionState.CardInserted, nameof(EnterPin));

        var correct = _atmService.ValidatePin(_currentCard!.CardNumber, pin);
        if (correct)
        {
            _state = SessionState.Authenticated;
            _failedPinAttempts = 0;
            return true;
        }

        _failedPinAttempts++;
        if (_failedPinAttempts >= MaxPinAttempts)
        {
            ResetSession();
            throw new CardBlockedException($"Maximum PIN attempts ({MaxPinAttempts}) exceeded; card retained.");
        }

        return false;
    }

    /// <summary>
    /// Lists the accounts available for the currently authenticated
    /// card.
    /// </summary>
    public IReadOnlyList<Account> GetAvailableAccounts()
    {
        RequireAuthenticatedOrFurther(nameof(GetAvailableAccounts));
        return _atmService.GetAccounts(_currentCard!.CardNumber);
    }

    /// <summary>
    /// Selects which account subsequent operations
    /// (balance/deposit/withdraw) will apply to. Must be one of the
    /// accounts linked to the inserted card.
    /// </summary>
    /// <remarks>
    /// May also be called again after an account has already been
    /// selected, letting an authenticated customer switch accounts
    /// mid-session without re-entering their PIN — the same way a real
    /// ATM's "go back to account selection" menu option works.
    /// </remarks>
    public void SelectAccount(string accountId)
    {
        if (_state != SessionState.Authenticated && _state != SessionState.AccountSelected)
        {
            throw new InvalidStateException(
                $"Cannot call SelectAccount() while in state {_state}; PIN must be validated first.");
        }

        var ownsAccount = _atmService.GetAccounts(_currentCard!.CardNumber)
            .Any(account => account.AccountId == accountId);
        if (!ownsAccount)
        {
            throw new AccountNotFoundException($"Account {accountId} is not available on this card.");
        }

        _currentAccountId = accountId;
        _state = SessionState.AccountSelected;
    }

    /// <summary>
    /// Returns the current balance of the selected account.
    /// </summary>
    public int CheckBalance()
    {
        RequireState(SessionState.AccountSelected, nameof(CheckBalance));
        return _atmService.CheckBalance(_currentAccountId!);
    }

    /// <summary>
    /// Deposits <paramref name="amount"/> (whole dollars) into the
    /// selected account.
    /// </summary>
    public Transaction Deposit(int amount)
    {
        RequireState(SessionState.AccountSelected, nameof(Deposit));
        return _atmService.Deposit(_currentAccountId!, amount);
    }

    /// <summary>
    /// Withdraws <paramref name="amount"/> (whole dollars) from the
    /// selected account.
    /// </summary>
    public Transaction Withdraw(int amount)
    {
        RequireState(SessionState.AccountSelected, nameof(Withdraw));
        return _atmService.Withdraw(_currentAccountId!, amount);
    }

    /// <summary>
    /// Ends the current session and returns the card, ready for the next
    /// customer. Safe to call from any state.
    /// </summary>
    public void EjectCard()
    {
        ResetSession();
    }

    private void RequireState(SessionState required, string operation)
    {
        if (_state != required)
        {
            throw new InvalidStateException(
                $"Cannot call {operation}() while in state {_state}; expected {required}.");
        }
    }

    private void RequireAuthenticatedOrFurther(string operation)
    {
        if (_state == SessionState.NoCard || _state == SessionState.CardInserted)
        {
            throw new InvalidStateException(
                $"Cannot call {operation}() while in state {_state}; PIN must be validated first.");
        }
    }

    private void ResetSession()
    {
        _currentCard = null;
        _currentAccountId = null;
        _failedPinAttempts = 0;
        _state = SessionState.NoCard;
    }
}
