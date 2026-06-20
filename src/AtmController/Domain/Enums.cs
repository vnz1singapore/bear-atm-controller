namespace Atm.Domain;

/// <summary>
/// The kind of operation a <see cref="Transaction"/> represents.
/// </summary>
public enum TransactionType
{
    Deposit,
    Withdrawal,
    BalanceInquiry
}

/// <summary>
/// The outcome of a <see cref="Transaction"/>.
/// </summary>
public enum TransactionStatus
{
    Success,
    Failed
}
