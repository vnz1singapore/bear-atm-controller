using Atm.Controllers;
using Atm.Domain;
using Atm.Exceptions;
using Atm.Gateways;
using Atm.Services;
using Xunit;

namespace Atm.Tests;

/// <summary>
/// End-to-end tests for <see cref="AtmController"/>, driving it the same
/// way a real caller (console UI, hardware integration, etc.) would:
/// through its public InsertCard / EnterPin / SelectAccount / operation
/// methods only.
/// </summary>
public class AtmControllerTests
{
    private const string CardNumber = "4111-1111-1111-1111";
    private const string CorrectPin = "1234";
    private const string WrongPin = "0000";
    private const string CheckingAccount = "CHK-001";
    private const string SavingsAccount = "SAV-001";
    private const int CheckingInitialBalance = 500;
    private const int SavingsInitialBalance = 1000;

    private readonly MockBankGateway _bankGateway;
    private readonly AtmController _controller;

    public AtmControllerTests()
    {
        _bankGateway = new MockBankGateway();
        _bankGateway.RegisterAccount(CheckingAccount, CheckingInitialBalance);
        _bankGateway.RegisterAccount(SavingsAccount, SavingsInitialBalance);
        _bankGateway.RegisterCard(CardNumber, CorrectPin, new[] { CheckingAccount, SavingsAccount });

        _controller = new AtmController(new AtmService(_bankGateway));
    }

    private void InsertCardAndAuthenticate()
    {
        _controller.InsertCard(new Card(CardNumber));
        _controller.EnterPin(CorrectPin);
    }

    // ------------------------------------------------------------
    // 1. Insert card successfully
    // ------------------------------------------------------------

    [Fact]
    public void InsertCard_Succeeds_WhenNoCardCurrentlyInserted()
    {
        var exception = Record.Exception(() => _controller.InsertCard(new Card(CardNumber)));

        Assert.Null(exception);
    }

    [Fact]
    public void InsertCard_Throws_WhenCardAlreadyInserted()
    {
        _controller.InsertCard(new Card(CardNumber));

        Assert.Throws<InvalidStateException>(() => _controller.InsertCard(new Card("5555-0000")));
    }

    // ------------------------------------------------------------
    // 2 & 3. Correct / incorrect PIN
    // ------------------------------------------------------------

    [Fact]
    public void EnterPin_ReturnsTrue_AndAuthenticates_WhenPinIsCorrect()
    {
        _controller.InsertCard(new Card(CardNumber));

        var result = _controller.EnterPin(CorrectPin);

        Assert.True(result);
        // Proves the session actually advanced: selecting an account now succeeds.
        var ex = Record.Exception(() => _controller.SelectAccount(CheckingAccount));
        Assert.Null(ex);
    }

    [Fact]
    public void EnterPin_ReturnsFalse_WhenPinIsIncorrect()
    {
        _controller.InsertCard(new Card(CardNumber));

        var result = _controller.EnterPin(WrongPin);

        Assert.False(result);
        // Session should still be stuck before account selection.
        Assert.Throws<InvalidStateException>(() => _controller.SelectAccount(CheckingAccount));
    }

    [Fact]
    public void EnterPin_Throws_WhenCardNotYetInserted()
    {
        Assert.Throws<InvalidStateException>(() => _controller.EnterPin(CorrectPin));
    }

    [Fact]
    public void EnterPin_AllowsRetry_BeforeAttemptLimitReached()
    {
        _controller.InsertCard(new Card(CardNumber));

        Assert.False(_controller.EnterPin(WrongPin));
        Assert.False(_controller.EnterPin(WrongPin));
        // Third attempt is correct -> should still succeed since the limit is 3 strikes.
        Assert.True(_controller.EnterPin(CorrectPin));
    }

    [Fact]
    public void EnterPin_ThrowsCardBlocked_AndResetsSession_WhenAttemptLimitExceeded()
    {
        _controller.InsertCard(new Card(CardNumber));
        _controller.EnterPin(WrongPin);
        _controller.EnterPin(WrongPin);

        Assert.Throws<CardBlockedException>(() => _controller.EnterPin(WrongPin));

        // Session was reset: even the correct PIN now fails because there's no card inserted.
        Assert.Throws<InvalidStateException>(() => _controller.EnterPin(CorrectPin));
    }

    // ------------------------------------------------------------
    // 4 & 5. Account selection
    // ------------------------------------------------------------

    [Fact]
    public void SelectAccount_Throws_WhenPinNotYetValidated()
    {
        _controller.InsertCard(new Card(CardNumber));

        Assert.Throws<InvalidStateException>(() => _controller.SelectAccount(CheckingAccount));
    }

    [Fact]
    public void SelectAccount_Succeeds_AfterPinValidated()
    {
        InsertCardAndAuthenticate();

        var exception = Record.Exception(() => _controller.SelectAccount(SavingsAccount));

        Assert.Null(exception);
        Assert.Equal(SavingsInitialBalance, _controller.CheckBalance());
    }

    [Fact]
    public void SelectAccount_Throws_WhenAccountNotLinkedToCard()
    {
        InsertCardAndAuthenticate();

        Assert.Throws<AccountNotFoundException>(() => _controller.SelectAccount("NOT-MY-ACCOUNT"));
    }

    [Fact]
    public void GetAvailableAccounts_ListsOnlyAccountsLinkedToCard()
    {
        InsertCardAndAuthenticate();

        var accounts = _controller.GetAvailableAccounts();

        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, a => a.AccountId == CheckingAccount);
        Assert.Contains(accounts, a => a.AccountId == SavingsAccount);
    }

    [Fact]
    public void SelectAccount_AllowsSwitchingAccounts_WithoutReEnteringPin()
    {
        InsertCardAndAuthenticate();
        _controller.SelectAccount(CheckingAccount);
        _controller.Withdraw(100);

        _controller.SelectAccount(SavingsAccount);

        Assert.Equal(SavingsInitialBalance, _controller.CheckBalance());
    }

    // ------------------------------------------------------------
    // 6. Check balance
    // ------------------------------------------------------------

    [Fact]
    public void CheckBalance_ReturnsAccountsCurrentBalance()
    {
        InsertCardAndAuthenticate();
        _controller.SelectAccount(CheckingAccount);

        Assert.Equal(CheckingInitialBalance, _controller.CheckBalance());
    }

    [Fact]
    public void CheckBalance_Throws_WhenNoAccountSelected()
    {
        InsertCardAndAuthenticate();

        Assert.Throws<InvalidStateException>(() => _controller.CheckBalance());
    }

    // ------------------------------------------------------------
    // 7. Deposit increases balance
    // ------------------------------------------------------------

    [Fact]
    public void Deposit_IncreasesBalance_ByDepositedAmount()
    {
        InsertCardAndAuthenticate();
        _controller.SelectAccount(CheckingAccount);

        var transaction = _controller.Deposit(150);

        Assert.Equal(CheckingInitialBalance + 150, _controller.CheckBalance());
        Assert.Equal(TransactionType.Deposit, transaction.Type);
        Assert.Equal(150, transaction.Amount);
        Assert.Equal(TransactionStatus.Success, transaction.Status);
    }

    [Fact]
    public void Deposit_Throws_WhenNoAccountSelected()
    {
        InsertCardAndAuthenticate();

        Assert.Throws<InvalidStateException>(() => _controller.Deposit(100));
    }

    // ------------------------------------------------------------
    // 8. Withdraw decreases balance
    // ------------------------------------------------------------

    [Fact]
    public void Withdraw_DecreasesBalance_ByWithdrawnAmount()
    {
        InsertCardAndAuthenticate();
        _controller.SelectAccount(CheckingAccount);

        var transaction = _controller.Withdraw(200);

        Assert.Equal(CheckingInitialBalance - 200, _controller.CheckBalance());
        Assert.Equal(TransactionType.Withdrawal, transaction.Type);
        Assert.Equal(200, transaction.Amount);
    }

    [Fact]
    public void Withdraw_FullBalance_LeavesZeroBalance()
    {
        InsertCardAndAuthenticate();
        _controller.SelectAccount(CheckingAccount);

        _controller.Withdraw(CheckingInitialBalance);

        Assert.Equal(0, _controller.CheckBalance());
    }

    [Fact]
    public void Withdraw_Throws_WhenNoAccountSelected()
    {
        InsertCardAndAuthenticate();

        Assert.Throws<InvalidStateException>(() => _controller.Withdraw(50));
    }

    // ------------------------------------------------------------
    // 9. Withdraw fails with insufficient funds
    // ------------------------------------------------------------

    [Fact]
    public void Withdraw_ThrowsInsufficientFunds_AndLeavesBalanceUnchanged_WhenOverdrawing()
    {
        InsertCardAndAuthenticate();
        _controller.SelectAccount(CheckingAccount);

        Assert.Throws<InsufficientFundsException>(() => _controller.Withdraw(CheckingInitialBalance + 1));
        Assert.Equal(CheckingInitialBalance, _controller.CheckBalance());
    }

    // ------------------------------------------------------------
    // 10. Zero or negative deposit/withdraw fails
    // ------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Deposit_Throws_WhenAmountIsZeroOrNegative(int invalidAmount)
    {
        InsertCardAndAuthenticate();
        _controller.SelectAccount(CheckingAccount);

        Assert.Throws<InvalidAmountException>(() => _controller.Deposit(invalidAmount));
        Assert.Equal(CheckingInitialBalance, _controller.CheckBalance());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Withdraw_Throws_WhenAmountIsZeroOrNegative(int invalidAmount)
    {
        InsertCardAndAuthenticate();
        _controller.SelectAccount(CheckingAccount);

        Assert.Throws<InvalidAmountException>(() => _controller.Withdraw(invalidAmount));
        Assert.Equal(CheckingInitialBalance, _controller.CheckBalance());
    }

    // ------------------------------------------------------------
    // 11. Eject card resets session
    // ------------------------------------------------------------

    [Fact]
    public void EjectCard_ResetsSession_SoOperationsRequireANewCard()
    {
        InsertCardAndAuthenticate();
        _controller.SelectAccount(CheckingAccount);

        _controller.EjectCard();

        Assert.Throws<InvalidStateException>(() => _controller.CheckBalance());
        var exception = Record.Exception(() => _controller.InsertCard(new Card(CardNumber)));
        Assert.Null(exception);
    }

    [Fact]
    public void EjectCard_IsSafeToCall_FromAnyState()
    {
        var exception = Record.Exception(() => _controller.EjectCard());

        Assert.Null(exception);
    }

    // ------------------------------------------------------------
    // Full flow
    // ------------------------------------------------------------

    [Fact]
    public void FullHappyPathFlow_InsertCard_Authenticate_SelectAccount_Deposit_Withdraw_CheckBalance()
    {
        _controller.InsertCard(new Card(CardNumber));
        Assert.True(_controller.EnterPin(CorrectPin));
        _controller.SelectAccount(CheckingAccount);

        _controller.Deposit(100);
        _controller.Withdraw(50);

        Assert.Equal(CheckingInitialBalance + 100 - 50, _controller.CheckBalance());
    }
}
