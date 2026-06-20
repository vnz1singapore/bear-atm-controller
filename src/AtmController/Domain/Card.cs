namespace Atm.Domain;

/// <summary>
/// Represents a bank card inserted into the ATM.
/// </summary>
/// <remarks>
/// This is intentionally a thin, immutable value object. Today it is
/// constructed directly by callers (e.g. in tests, or eventually by
/// <see cref="Atm.Hardware.ICardReader"/>). In a future hardware
/// integration, a real <c>ICardReader</c> implementation would be
/// responsible for producing instances of this class from the physical
/// card, but <see cref="Atm.Controllers.AtmController"/> would not need
/// to change.
/// </remarks>
public sealed record Card
{
    public string CardNumber { get; }

    public Card(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            throw new ArgumentException("Card number must not be null or blank.", nameof(cardNumber));
        }

        CardNumber = cardNumber;
    }

    public override string ToString() => $"Card {{ CardNumber = {Mask(CardNumber)} }}";

    private static string Mask(string value) =>
        value.Length <= 4 ? "****" : $"****{value[^4..]}";
}
