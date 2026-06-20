# ATM Controller (C# / .NET 8)

A simple ATM controller implemented as a .NET 8 class library: no UI, no
hardware, no real bank integration — just the controller logic and its
unit tests, structured so that those things could be plugged in later
without reworking the core design.

## 1. Project Overview

The project implements the required flow:

```
Insert Card -> Enter PIN -> Select Account -> Check Balance / Deposit / Withdraw
```

It is organized into four layers, each with a single responsibility:

```
AtmController   session state machine (enforces the required flow/order)
      |
AtmService      business rules (PIN check delegation, deposit/withdraw
      |         rules, transaction log)
      |
IBankGateway    interface — the only thing that knows accounts/PINs exist
      |
MockBankGateway in-memory implementation used today, standing in for a
                real bank integration
```

`Hardware/ICardReader` and `Hardware/ICashDispenser` are defined as
forward-looking interfaces for a future hardware integration; they are
not wired into `AtmController` today, since card-reader and
cash-dispenser hardware are explicitly out of scope (see "Future
Extension Points" below for how they'd plug in).

Domain objects (`Card`, `Account`, `Transaction`) are immutable
`record` types shared across the layers. Only whole-dollar `int` amounts
are supported, per the assignment's simplification (only $1 bills exist,
no cents), and zero/negative amounts are rejected.

## 2. How to Build

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build
```

This builds both the `AtmController` library and the
`AtmController.Tests` test project via `AtmController.sln`.

## 3. How to Run Tests

```bash
dotnet test
```

This runs the full xUnit suite — 48 test cases across three test
classes, all passing:

- **`AtmControllerTests`** (29 cases) — end-to-end session/flow tests,
  driven only through the controller's public API. Covers every
  required case from the assignment (insert card, correct PIN,
  incorrect PIN, account selection before/after PIN validation, balance
  check, deposit increases balance, withdraw decreases balance, withdraw
  with insufficient funds, zero/negative amounts, eject card resets
  session) plus session edge cases (PIN retry limit and card blocking,
  switching accounts mid-session, calling operations out of order).
- **`AtmServiceTests`** (12 cases) — business-rule tests against
  `AtmService` directly, independent of session state.
- **`MockBankGatewayTests`** (7 cases) — contract tests for the gateway
  implementation itself (the same tests a future real gateway
  implementation should also satisfy).

## 4. Architecture Explanation

**Layering / SOLID.** `AtmController` only knows about session state
(what step the customer is on) and delegates every business rule to
`AtmService`. `AtmService` only knows about ATM-side rules (a deposit
must be positive, a withdrawal can't exceed the balance, etc.) and
delegates all actual account access to `IBankGateway`. Neither
`AtmController` nor `AtmService` reference `MockBankGateway` directly —
they depend only on the `IBankGateway` interface (dependency inversion),
which is what makes swapping in a real bank integration a one-class
change.

**PIN handling.** A real bank system should never expose the actual
PIN, only answer "is this PIN correct?". `IBankGateway` reflects that
directly: `ValidatePin(cardNumber, pin)` returns a `bool`, and there is
no member anywhere in the codebase that returns or stores a PIN for
retrieval. `MockBankGateway` is the only class that holds PIN values at
all, and it only ever compares against them.

**Why `AtmController.EnterPin` returns a `bool` instead of throwing.**
An incorrect PIN is an expected, recoverable outcome (the customer is
allowed to retry), not an exceptional one — mirroring the "yes/no"
contract from `IBankGateway`. Truly exceptional conditions (calling a
method out of session order, exceeding the retry limit, insufficient
funds, an invalid amount, selecting an account that isn't on the card)
are modeled as unchecked exceptions under a common `AtmException` base
type, so callers can either handle them granularly or catch all ATM
failures generically.

**PIN retry limit.** After `AtmController.MaxPinAttempts` (3) consecutive
incorrect attempts, the controller throws `CardBlockedException` and
resets the session, mirroring real ATM behavior (card retained) rather
than allowing unlimited guesses.

**Account switching.** Once authenticated, `SelectAccount` can be called
again to switch to a different linked account without re-entering the
PIN — matching how a real ATM's "back to account selection" option
behaves — rather than forcing full re-authentication per account.

**Session state, not a god object.** `AtmController` uses a private
`SessionState` enum (`NoCard`, `CardInserted`, `Authenticated`,
`AccountSelected`) to enforce that operations happen in the right order
(e.g. you cannot `Withdraw()` before `SelectAccount()`), throwing
`InvalidStateException` otherwise. This keeps the required flow an
explicit, testable contract rather than an implicit assumption.

**Transaction log.** `AtmService` keeps an in-memory, append-only log of
successful deposits/withdrawals as `Transaction` records (returned to
the caller as a receipt, and queryable via `GetTransactionHistory()`).
Failed operations (insufficient funds, invalid amount) are not logged as
transactions, since nothing actually happened to the account.

**Immutable domain types.** `Card`, `Account`, and `Transaction` are
`sealed record` types: immutable, with value-based equality generated by
the compiler, which keeps them easy to reason about and to assert
against in tests.

**Single-session controller.** One `AtmController` instance models one
physical ATM serving one customer at a time, the same way a real ATM
works; it is not designed to be thread-safe or to multiplex concurrent
sessions.

## 5. Future Extension Points

- **Real bank API.** Implement `IBankGateway` (e.g. `RestBankGateway`,
  `GrpcBankGateway`) against the real bank's API and construct
  `AtmService` with it instead of `MockBankGateway`. No change needed in
  `AtmService` or `AtmController`.
- **Real card reader.** Implement `ICardReader` against the physical
  device; a caller would invoke `ICardReader.ReadCard()` and pass the
  resulting `Card` straight into `AtmController.InsertCard(card)` — the
  controller has no hardware-specific code to change.
- **Real cash dispenser.** Implement `ICashDispenser` against the
  physical device; a caller would invoke it *after*
  `AtmController.Withdraw` has already succeeded, to physically dispense
  the bills for a withdrawal the bank has already agreed to debit,
  without any change to the controller or service layer.
- **Multiple currencies / denominations.** Today amounts are plain
  whole-dollar `int`s per the assignment's simplification. A `Money`
  value type (amount + currency, or a bill-denomination breakdown for
  the dispenser) could replace `int` at the API boundary if that's ever
  needed, without touching the layering.
- **Persistence / auditing.** `AtmService.GetTransactionHistory()` is
  in-memory today; swapping it for a persisted transaction log (e.g.
  writing to a database) is isolated to that one class.
- **UI.** Any UI (console, GUI, web) is just a caller of
  `AtmController`'s public methods and was intentionally kept out of
  scope.

## Repository Structure

```
src/AtmController/
  Controllers/   AtmController.cs
  Services/      AtmService.cs
  Gateways/      IBankGateway.cs, MockBankGateway.cs
  Hardware/      ICardReader.cs, ICashDispenser.cs
  Domain/        Card.cs, Account.cs, Transaction.cs, Enums.cs
  Exceptions/    AtmException.cs (base + all derived exception types)
  AtmController.csproj
tests/AtmController.Tests/
  AtmControllerTests.cs
  AtmServiceTests.cs
  MockBankGatewayTests.cs
  AtmController.Tests.csproj
AtmController.sln
README.md
.gitignore
```
