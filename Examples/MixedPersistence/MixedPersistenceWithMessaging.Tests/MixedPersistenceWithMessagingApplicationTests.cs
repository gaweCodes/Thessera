using GaWeCodes.Thessera.Application.Results;
using GaWeCodes.Thessera.Tests;
using MixedPersistenceWithMessaging;

[Collection(BrokerAndDatabaseCollection.Name)]
public sealed class MixedPersistenceWithMessagingApplicationTests(PostgreSqlFixture postgres, RabbitMqFixture rabbit)
{
    [Fact]
    public async Task CrudFlow_PublishesAndReceivesRabbitMqMessages_ForBothAggregates()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        try
        {
            await using var app = await MixedPersistenceWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken);

            var reading = await app.CreateReadingAsync(8, TestContext.Current.CancellationToken);
            Assert.True(reading.IsSuccess);
            var updatedReading = await app.UpdateReadingAsync(reading.Value.Reading.Id, 13, TestContext.Current.CancellationToken);
            Assert.True(updatedReading.IsSuccess);
            var deletedReading = await app.DeleteReadingAsync(reading.Value.Reading.Id, TestContext.Current.CancellationToken);
            Assert.True(deletedReading.IsSuccess);

            var account = await app.OpenAccountAsync(100m, TestContext.Current.CancellationToken);
            Assert.True(account.IsSuccess);
            var deposited = await app.DepositAsync(account.Value.Account.Id, 25m, TestContext.Current.CancellationToken);
            Assert.True(deposited.IsSuccess);
            var withdrawn = await app.WithdrawAsync(account.Value.Account.Id, 125m, TestContext.Current.CancellationToken);
            Assert.True(withdrawn.IsSuccess);
            var closed = await app.CloseAccountAsync(account.Value.Account.Id, TestContext.Current.CancellationToken);
            Assert.True(closed.IsSuccess);

            var logPath = Path.Combine(artifactDirectory, "received-events.log");
            Assert.True(File.Exists(logPath));

            var content = await File.ReadAllTextAsync(logPath, TestContext.Current.CancellationToken);
            Assert.Contains("mixed-persistence.reading-created", content, StringComparison.Ordinal);
            Assert.Contains("mixed-persistence.reading-updated", content, StringComparison.Ordinal);
            Assert.Contains("mixed-persistence.reading-deleted", content, StringComparison.Ordinal);
            Assert.Contains("mixed-persistence.account-opened", content, StringComparison.Ordinal);
            Assert.Contains("mixed-persistence.account-deposited", content, StringComparison.Ordinal);
            Assert.Contains("mixed-persistence.account-withdrawn", content, StringComparison.Ordinal);
            Assert.Contains("mixed-persistence.account-closed", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FailedAccountTransaction_PublishesNoAccountEvent_AndDoesNotAffectReadingPublishing()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        try
        {
            await using var app = await MixedPersistenceWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken);

            var opened = await app.OpenAccountAsync(10m, TestContext.Current.CancellationToken);
            Assert.True(opened.IsSuccess);

            var logPath = Path.Combine(artifactDirectory, "received-events.log");
            await WaitForFileToContainAsync(logPath, "mixed-persistence.account-opened", TestContext.Current.CancellationToken);

            var failedWithdrawal = await app.WithdrawAsync(opened.Value.Account.Id, 999m, TestContext.Current.CancellationToken);
            Assert.False(failedWithdrawal.IsSuccess);
            Assert.Equal("account.insufficient-funds", failedWithdrawal.Failures[0].Code);

            // A business-rule failure never commits the EF Core transaction, so Wolverine's
            // outbox never gets a message to flush - no new account event is published. Waiting
            // for the (independently published) reading event gives the failed withdrawal enough
            // time to have shown up too, had it wrongly published anything.
            var reading = await app.CreateReadingAsync(5, TestContext.Current.CancellationToken);
            Assert.True(reading.IsSuccess);

            var afterReading = await WaitForFileToContainAsync(
                logPath, "mixed-persistence.reading-created", TestContext.Current.CancellationToken);
            Assert.Equal(1, CountOccurrences(afterReading, "\"RoutingKey\": \"mixed-persistence.account-"));
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    [Fact]
    public async Task FailedReadingCreate_PublishesNoReadingEvent_AndDoesNotAffectAccountPublishing()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        try
        {
            await using var app = await MixedPersistenceWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken);

            var failedCreate = await app.CreateReadingAsync(0, TestContext.Current.CancellationToken);
            Assert.False(failedCreate.IsSuccess);
            Assert.Equal("reading.value.not-positive", failedCreate.Failures[0].Code);

            var logPath = Path.Combine(artifactDirectory, "received-events.log");

            // A rejected reading is never appended to the Marten stream in the first place, so
            // there is nothing for the outbox to publish - the account side is unaffected.
            var opened = await app.OpenAccountAsync(10m, TestContext.Current.CancellationToken);
            Assert.True(opened.IsSuccess);

            var content = await WaitForFileToContainAsync(
                logPath, "mixed-persistence.account-opened", TestContext.Current.CancellationToken);
            Assert.DoesNotContain("mixed-persistence.reading-created", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Withdraw_WithInsufficientFunds_ReturnsBusinessRuleFailure()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        try
        {
            await using var app = await MixedPersistenceWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken);

            var opened = await app.OpenAccountAsync(10m, TestContext.Current.CancellationToken);
            var result = await app.WithdrawAsync(opened.Value.Account.Id, 20m, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Single(result.Failures);
            Assert.Equal("account.insufficient-funds", result.Failures[0].Code);
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Close_WithARemainingBalance_ReturnsBusinessRuleFailure()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        try
        {
            await using var app = await MixedPersistenceWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken);

            var opened = await app.OpenAccountAsync(10m, TestContext.Current.CancellationToken);
            var result = await app.CloseAccountAsync(opened.Value.Account.Id, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Single(result.Failures);
            Assert.Equal("account.close.balance-not-zero", result.Failures[0].Code);
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Create_ReadingWithNonPositiveValue_ReturnsValidationFailure()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        try
        {
            await using var app = await MixedPersistenceWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken);

            var result = await app.CreateReadingAsync(0, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Single(result.Failures);
            Assert.Equal("reading.value.not-positive", result.Failures[0].Code);
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Update_ReadingWithUnknownId_ReturnsNotFound()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        try
        {
            await using var app = await MixedPersistenceWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken);

            var result = await app.UpdateReadingAsync(999, 21, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Single(result.Failures);
            Assert.Equal("reading.not_found", result.Failures[0].Code);
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StartAsync_RebuildsTheAccountReadModelFromWriteRowsWrittenByAnEarlierProcess()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        Result<AccountOperationResponse> opened = default!;

        try
        {
            await using (var first = await MixedPersistenceWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken))
            {
                opened = await first.OpenAccountAsync(42m, TestContext.Current.CancellationToken);
                Assert.True(opened.IsSuccess);
            }

            try
            {
                await using var second = await MixedPersistenceWithMessagingApplication.StartAsync(
                    postgres.ConnectionString,
                    rabbit.ConnectionUri,
                    artifactDirectory,
                    TestContext.Current.CancellationToken);

                var listed = await second.ListAccountsAsync(TestContext.Current.CancellationToken);
                Assert.True(listed.IsSuccess);
                Assert.Contains(listed.Value.Accounts, account => account.Id == opened.Value.Account.Id && account.Balance == 42m);
            }
            finally
            {
                await using var cleanup = await MixedPersistenceWithMessagingApplication.StartAsync(
                    postgres.ConnectionString,
                    rabbit.ConnectionUri,
                    artifactDirectory,
                    TestContext.Current.CancellationToken);
                await cleanup.WithdrawAsync(opened.Value.Account.Id, 42m, TestContext.Current.CancellationToken);
                await cleanup.CloseAccountAsync(opened.Value.Account.Id, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task StartAsync_RebuildsTheReadingReadModelFromEventStreamsWrittenByAnEarlierProcess()
    {
        Assert.SkipUnless(postgres.Available, postgres.SkipReason);
        Assert.SkipUnless(rabbit.Available, rabbit.SkipReason);

        var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        Result<ReadingOperationResponse> created = default!;

        try
        {
            await using (var first = await MixedPersistenceWithMessagingApplication.StartAsync(
                postgres.ConnectionString,
                rabbit.ConnectionUri,
                artifactDirectory,
                TestContext.Current.CancellationToken))
            {
                created = await first.CreateReadingAsync(42, TestContext.Current.CancellationToken);
                Assert.True(created.IsSuccess);
            }

            try
            {
                await using var second = await MixedPersistenceWithMessagingApplication.StartAsync(
                    postgres.ConnectionString,
                    rabbit.ConnectionUri,
                    artifactDirectory,
                    TestContext.Current.CancellationToken);

                var listed = await second.ListReadingsAsync(TestContext.Current.CancellationToken);
                Assert.True(listed.IsSuccess);
                Assert.Contains(listed.Value.Readings, reading => reading.Id == created.Value.Reading.Id && reading.Value == 42);
            }
            finally
            {
                await using var cleanup = await MixedPersistenceWithMessagingApplication.StartAsync(
                    postgres.ConnectionString,
                    rabbit.ConnectionUri,
                    artifactDirectory,
                    TestContext.Current.CancellationToken);
                await cleanup.DeleteReadingAsync(created.Value.Reading.Id, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    private static async Task<string> WaitForFileToContainAsync(string path, string expected, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            if (File.Exists(path))
            {
                var content = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                if (content.Contains(expected, StringComparison.Ordinal))
                {
                    return content;
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"'{expected}' was not written to '{path}' in time.");
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }
}
