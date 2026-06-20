namespace Atm.Hardware;

/// <summary>
/// Abstraction over the physical device that dispenses cash.
/// </summary>
/// <remarks>
/// Not used by <see cref="Atm.Controllers.AtmController"/> directly
/// today — physical dispensing is out of scope for this assignment. A
/// future caller would invoke this <i>after</i>
/// <c>AtmController.Withdraw</c> has already succeeded, to physically
/// dispense the bills for a withdrawal the bank has already agreed to
/// debit, without any change required to the controller or service
/// layer.
/// </remarks>
public interface ICashDispenser
{
    /// <summary>
    /// Whether the dispenser currently holds enough bills to fulfill a
    /// withdrawal of <paramref name="amount"/> whole dollars.
    /// </summary>
    bool HasSufficientCash(int amount);

    /// <summary>
    /// Physically dispenses <paramref name="amount"/> whole dollars,
    /// using only 1-dollar bills per this project's simplification.
    /// </summary>
    void Dispense(int amount);
}
