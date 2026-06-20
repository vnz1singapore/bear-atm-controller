using Atm.Exceptions;
using Atm.Gateways;
using Xunit;

namespace Atm.Tests;

/// <summary>
/// Unit tests for <see cref="MockBankGateway"/>, the in-memory stand-in
/// for a real bank used today. These tests exist mainly to pin down the
/// contract that any future real <see cref="IBankGateway"/>
/// implementation (e.g. a REST-based one) must also honor.
/// </summary>
public class MockBankGatewayTests
{
    private readonly MockBankGateway _gateway;

    public MockBankGatewayTests()
    {
        _gateway = new MockBankGateway();
        _gateway.RegisterAccount("ACC-1", 100);
        _gateway.RegisterCard("CARD-1", "1111", new[] { "ACC-1" });
    }

    [Fact]
    public void ValidatePin_NeverExposesActualPin_OnlyTrueOrFalse()
    {
        Assert.True(_gateway.ValidatePin("CARD-1", "1111"));
        Assert.False(_gateway.ValidatePin("CARD-1", "9999"));
        Assert.False(_gateway.ValidatePin("UNKNOWN-CARD", "1111"));
    }

    [Fact]
    public void GetAccounts_ReturnsEmptyList_ForUnknownCard()
    {
        Assert.Empty(_gateway.GetAccounts("UNKNOWN-CARD"));
    }

    [Fact]
    public void GetBalance_Throws_ForUnknownAccount()
    {
        Assert.Throws<AccountNotFoundException>(() => _gateway.GetBalance("NO-SUCH-ACCOUNT"));
    }

    [Fact]
    public void Deposit_IncreasesBalance()
    {
        _gateway.Deposit("ACC-1", 25);

        Assert.Equal(125, _gateway.GetBalance("ACC-1"));
    }

    [Fact]
    public void Withdraw_DecreasesBalance_WhenFundsAreSufficient()
    {
        _gateway.Withdraw("ACC-1", 40);

        Assert.Equal(60, _gateway.GetBalance("ACC-1"));
    }

    [Fact]
    public void Withdraw_Throws_WhenOverdrawing()
    {
        Assert.Throws<InsufficientFundsException>(() => _gateway.Withdraw("ACC-1", 101));
        Assert.Equal(100, _gateway.GetBalance("ACC-1"));
    }

    [Fact]
    public void GetAccounts_ReflectsLiveBalance_NotAStaleSnapshot()
    {
        _gateway.Deposit("ACC-1", 10);

        var accounts = _gateway.GetAccounts("CARD-1");

        Assert.Equal(110, accounts[0].Balance);
    }
}
