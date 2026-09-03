using GaWeCodes.Thessera.Tests;
using GaWeCodes.Thessera.Application.Results;
using MixedPersistence;

namespace MixedPersistence.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class MixedPersistenceApplicationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task BothStores_CommitIndependentlyInTheSameHost()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var reading = await app.CreateReadingAsync(8, TestContext.Current.CancellationToken);
        Assert.True(reading.IsSuccess);

        var account = await app.OpenAccountAsync(100m, TestContext.Current.CancellationToken);
        Assert.True(account.IsSuccess);

        var readings = await app.ListReadingsAsync(TestContext.Current.CancellationToken);
        Assert.True(readings.IsSuccess);
        Assert.Contains(readings.Value.Readings, r => r.Id == reading.Value.Reading.Id);

        var accounts = await app.ListAccountsAsync(TestContext.Current.CancellationToken);
        Assert.True(accounts.IsSuccess);
        Assert.Contains(accounts.Value.Accounts, a => a.Id == account.Value.Account.Id);
    }

    [Fact]
    public async Task Reading_CrudFlow_UsesTheMartenAncillaryStore()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var created = await app.CreateReadingAsync(8, TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess);
        Assert.Single(created.Value.DomainEvents);

        var updated = await app.UpdateReadingAsync(created.Value.Reading.Id, 13, TestContext.Current.CancellationToken);
        Assert.True(updated.IsSuccess);
        Assert.Equal(13, updated.Value.Reading.Value);

        var deleted = await app.DeleteReadingAsync(created.Value.Reading.Id, TestContext.Current.CancellationToken);
        Assert.True(deleted.IsSuccess);
        Assert.True(deleted.Value.Reading.IsDeleted);
    }

    [Fact]
    public async Task Account_CrudFlow_UsesTheEfCoreMainStore()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var opened = await app.OpenAccountAsync(50m, TestContext.Current.CancellationToken);
        Assert.True(opened.IsSuccess);
        Assert.Equal(50m, opened.Value.Account.Balance);

        var deposited = await app.DepositAsync(opened.Value.Account.Id, 25m, TestContext.Current.CancellationToken);
        Assert.True(deposited.IsSuccess);
        Assert.Equal(75m, deposited.Value.Account.Balance);

        var withdrawn = await app.WithdrawAsync(opened.Value.Account.Id, 75m, TestContext.Current.CancellationToken);
        Assert.True(withdrawn.IsSuccess);
        Assert.Equal(0m, withdrawn.Value.Account.Balance);

        var closed = await app.CloseAccountAsync(opened.Value.Account.Id, TestContext.Current.CancellationToken);
        Assert.True(closed.IsSuccess);
        Assert.True(closed.Value.Account.IsClosed);
    }

    [Fact]
    public async Task Withdraw_WithInsufficientFunds_ReturnsBusinessRuleFailure()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);
        var opened = await app.OpenAccountAsync(10m, TestContext.Current.CancellationToken);

        var result = await app.WithdrawAsync(opened.Value.Account.Id, 20m, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Failures);
        Assert.Equal("account.insufficient-funds", result.Failures[0].Code);
    }

    [Fact]
    public async Task Close_WithARemainingBalance_ReturnsBusinessRuleFailure()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);
        var opened = await app.OpenAccountAsync(10m, TestContext.Current.CancellationToken);

        var result = await app.CloseAccountAsync(opened.Value.Account.Id, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Failures);
        Assert.Equal("account.close.balance-not-zero", result.Failures[0].Code);
    }

    [Fact]
    public async Task Deposit_IntoAClosedAccount_ReturnsConflictFailure()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);
        var opened = await app.OpenAccountAsync(0m, TestContext.Current.CancellationToken);
        await app.CloseAccountAsync(opened.Value.Account.Id, TestContext.Current.CancellationToken);

        var result = await app.DepositAsync(opened.Value.Account.Id, 5m, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Failures);
        Assert.Equal("account.closed", result.Failures[0].Code);
    }

    [Fact]
    public async Task Open_WithANegativeBalance_ReturnsValidationFailure()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var result = await app.OpenAccountAsync(-1m, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Failures);
        Assert.Equal("account.opening-balance.negative", result.Failures[0].Code);
    }

    [Fact]
    public async Task Deposit_WithUnknownAccountId_ReturnsNotFound()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var result = await app.DepositAsync(999, 5m, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Failures);
        Assert.Equal("account.not_found", result.Failures[0].Code);
    }

    [Fact]
    public async Task Create_ReadingWithNonPositiveValue_ReturnsValidationFailure()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var result = await app.CreateReadingAsync(0, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Failures);
        Assert.Equal("reading.value.not-positive", result.Failures[0].Code);
    }

    [Fact]
    public async Task Update_ReadingWithUnknownId_ReturnsNotFound()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        await using var app = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

        var result = await app.UpdateReadingAsync(999, 21, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Failures);
        Assert.Equal("reading.not_found", result.Failures[0].Code);
    }

    [Fact]
    public async Task StartAsync_RebuildsTheAccountReadModelFromWriteRowsWrittenByAnEarlierProcess()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        Result<AccountOperationResponse> opened;
        await using (var first = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken))
        {
            opened = await first.OpenAccountAsync(42m, TestContext.Current.CancellationToken);
            Assert.True(opened.IsSuccess);
        }

        try
        {
            await using var second = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

            var listed = await second.ListAccountsAsync(TestContext.Current.CancellationToken);
            Assert.True(listed.IsSuccess);
            Assert.Contains(listed.Value.Accounts, account => account.Id == opened.Value.Account.Id && account.Balance == 42m);
        }
        finally
        {
            await using var cleanup = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);
            await cleanup.WithdrawAsync(opened.Value.Account.Id, 42m, TestContext.Current.CancellationToken);
            await cleanup.CloseAccountAsync(opened.Value.Account.Id, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task StartAsync_RebuildsTheReadingReadModelFromEventStreamsWrittenByAnEarlierProcess()
    {
        Assert.SkipUnless(fixture.Available, fixture.SkipReason);

        Result<ReadingOperationResponse> created;
        await using (var first = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken))
        {
            created = await first.CreateReadingAsync(42, TestContext.Current.CancellationToken);
            Assert.True(created.IsSuccess);
        }

        try
        {
            await using var second = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);

            var listed = await second.ListReadingsAsync(TestContext.Current.CancellationToken);
            Assert.True(listed.IsSuccess);
            Assert.Contains(listed.Value.Readings, reading => reading.Id == created.Value.Reading.Id && reading.Value == 42);
        }
        finally
        {
            await using var cleanup = await MixedPersistenceApplication.StartAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);
            await cleanup.DeleteReadingAsync(created.Value.Reading.Id, TestContext.Current.CancellationToken);
        }
    }
}
