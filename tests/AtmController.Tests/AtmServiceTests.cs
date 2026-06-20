using Atm.Domain;
using Atm.Exceptions;
using Atm.Gateways;
using Atm.Services;
using Xunit;

namespace Atm.Tests;

/// <summary>
/// Unit tests for <see cref="AtmService"/> in isolation, using a real
/// <see cref="MockBankGateway"/> but bypassing
/// <see cref="Atm.Controllers.AtmController"/> entirely — these exercise
/// the business rules directly regardless of session/state-machine
/// concerns.
/// </summary>
public class AtmServiceTests
{
    private const string CardNumber = "CARD-1";
    private const string Pin = "9999";
    private const string AccountId = "ACC-1";

    private readonly MockBankGateway _bankGateway;
    private readonly AtmService _atmService;

    public AtmServiceTests()
    {
        _bankGateway = new MockBankGateway();
        _bankGateway.RegisterAccount(AccountId, 300);
        _bankGateway.RegisterCard(CardNumber, Pin, new[] { AccountId });
        _atmService = new AtmService(_bankGateway);
    }

    [Fact]
    public void ValidatePin_ReturnsTrue_ForCorrectPin()
    {
        Assert.True(_atmService.ValidatePin(CardNumber, Pin));
    }

    [Fact]
    public void ValidatePin_ReturnsFalse_ForIncorrectPin_WithoutThrowing()
    {
        Assert.False(_atmService.ValidatePin(CardNumber, "0000"));
    }

    [Fact]
    public void GetAccounts_ReturnsAccountsLinkedToCard()
    {
        var accounts = _atmService.GetAccounts(CardNumber);

        Assert.Single(accounts);
        Assert.Equal(AccountId, accounts[0].AccountId);
    }

    [Fact]
    public void CheckBalance_ReturnsGatewaysCurrentBalance()
    {
        Assert.Equal(300, _atmService.CheckBalance(AccountId));
    }

    [Fact]
    public void Deposit_CreditsAccount_AndRecordsSuccessfulTransaction()
    {
        var transaction = _atmService.Deposit(AccountId, 50);

        Assert.Equal(350, _atmService.CheckBalance(AccountId));
        Assert.Equal(TransactionType.Deposit, transaction.Type);
        Assert.Equal(50, transaction.Amount);
        Assert.Equal(AccountId, transaction.AccountId);
        Assert.Equal(TransactionStatus.Success, transaction.Status);
        Assert.Contains(transaction, _atmService.GetTransactionHistory());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Deposit_Throws_ForNonPositiveAmounts(int invalidAmount)
    {
        Assert.Throws<InvalidAmountException>(() => _atmService.Deposit(AccountId, invalidAmount));
    }

    [Fact]
    public void Withdraw_DebitsAccount_WhenFundsAreSufficient()
    {
        var transaction = _atmService.Withdraw(AccountId, 120);

        Assert.Equal(180, _atmService.CheckBalance(AccountId));
        Assert.Equal(TransactionType.Withdrawal, transaction.Type);
        Assert.Equal(120, transaction.Amount);
    }

    [Fact]
    public void Withdraw_Throws_AndLeavesBalanceUnchanged_WhenOverdrawing()
    {
        Assert.Throws<InsufficientFundsException>(() => _atmService.Withdraw(AccountId, 301));

        Assert.Equal(300, _atmService.CheckBalance(AccountId));
        Assert.Empty(_atmService.GetTransactionHistory());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Withdraw_Throws_ForNonPositiveAmounts(int invalidAmount)
    {
        Assert.Throws<InvalidAmountException>(() => _atmService.Withdraw(AccountId, invalidAmount));
    }

    [Fact]
    public void TransactionHistory_AccumulatesOnlySuccessfulOperations_InOrder()
    {
        _atmService.Deposit(AccountId, 10);
        _atmService.Withdraw(AccountId, 5);

        var history = _atmService.GetTransactionHistory();

        Assert.Equal(2, history.Count);
        Assert.Equal(TransactionType.Deposit, history[0].Type);
        Assert.Equal(TransactionType.Withdrawal, history[1].Type);
    }
}
