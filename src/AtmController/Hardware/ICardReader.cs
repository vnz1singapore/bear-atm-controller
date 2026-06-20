using Atm.Domain;

namespace Atm.Hardware;

/// <summary>
/// Abstraction over the physical (or virtual) device that reads a card
/// and produces a <see cref="Card"/> domain object.
/// </summary>
/// <remarks>
/// Not used by <see cref="Atm.Controllers.AtmController"/> directly
/// today — card reading is out of scope for this assignment. A future
/// caller (a real hardware integration) would call <see cref="ReadCard"/>
/// and pass the resulting <see cref="Card"/> straight into
/// <c>AtmController.InsertCard</c>, with no change required to the
/// controller itself.
/// </remarks>
public interface ICardReader
{
    /// <summary>
    /// Reads whatever card is currently present in the reader.
    /// </summary>
    Card ReadCard();

    /// <summary>
    /// Physically returns the card to the customer.
    /// </summary>
    void EjectCard();
}
