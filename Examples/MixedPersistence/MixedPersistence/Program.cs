using System.Text.Json;
using GaWeCodes.Thessera.Application.Results;
using MixedPersistence;

var connectionString = Environment.GetEnvironmentVariable("THESSERA_EXAMPLE_POSTGRES")
    ?? MixedPersistenceApplication.DefaultConnectionString;

await using var app = await MixedPersistenceApplication.StartAsync(connectionString);

while (true)
{
    Console.WriteLine();
    Console.WriteLine("-- Reading (event-sourced, Marten) --");
    Console.WriteLine("1) Create reading");
    Console.WriteLine("2) List readings");
    Console.WriteLine("3) Update reading");
    Console.WriteLine("4) Delete reading");
    Console.WriteLine("-- Account (state-stored, EF Core) --");
    Console.WriteLine("5) Open account");
    Console.WriteLine("6) Deposit");
    Console.WriteLine("7) Withdraw");
    Console.WriteLine("8) Close account");
    Console.WriteLine("9) List accounts");
    Console.WriteLine("10) Rebuild read models");
    Console.WriteLine("0) Exit");
    Console.Write("Select: ");

    var selection = Console.ReadLine();
    switch (selection)
    {
        case "1":
            Console.Write("Value: ");
            if (int.TryParse(Console.ReadLine(), out var createValue))
            {
                Print(await app.CreateReadingAsync(createValue));
            }
            else
            {
                PrintEnvelope(ResultEnvelope.FromFailure("Create", "value.invalid", "Enter a whole number."));
            }
            break;
        case "2":
            Print(await app.ListReadingsAsync());
            break;
        case "3":
            Console.Write("Reading id: ");
            var updateIdText = Console.ReadLine();
            Console.Write("New value: ");
            var updateValueText = Console.ReadLine();
            if (int.TryParse(updateIdText, out var updateId) && int.TryParse(updateValueText, out var updateValue))
            {
                Print(await app.UpdateReadingAsync(updateId, updateValue));
            }
            else
            {
                PrintEnvelope(ResultEnvelope.FromFailure("Update", "value.invalid", "Enter a valid id and whole number."));
            }
            break;
        case "4":
            Console.Write("Reading id: ");
            if (int.TryParse(Console.ReadLine(), out var deleteId))
            {
                Print(await app.DeleteReadingAsync(deleteId));
            }
            else
            {
                PrintEnvelope(ResultEnvelope.FromFailure("Delete", "id.invalid", "Enter a valid id."));
            }
            break;
        case "5":
            Console.Write("Initial balance: ");
            if (decimal.TryParse(Console.ReadLine(), out var initialBalance))
            {
                Print(await app.OpenAccountAsync(initialBalance));
            }
            else
            {
                PrintEnvelope(ResultEnvelope.FromFailure("Open", "value.invalid", "Enter a decimal amount."));
            }
            break;
        case "6":
            Console.Write("Account id: ");
            var depositIdText = Console.ReadLine();
            Console.Write("Amount: ");
            var depositAmountText = Console.ReadLine();
            if (int.TryParse(depositIdText, out var depositId) && decimal.TryParse(depositAmountText, out var depositAmount))
            {
                Print(await app.DepositAsync(depositId, depositAmount));
            }
            else
            {
                PrintEnvelope(ResultEnvelope.FromFailure("Deposit", "value.invalid", "Enter a valid id and amount."));
            }
            break;
        case "7":
            Console.Write("Account id: ");
            var withdrawIdText = Console.ReadLine();
            Console.Write("Amount: ");
            var withdrawAmountText = Console.ReadLine();
            if (int.TryParse(withdrawIdText, out var withdrawId) && decimal.TryParse(withdrawAmountText, out var withdrawAmount))
            {
                Print(await app.WithdrawAsync(withdrawId, withdrawAmount));
            }
            else
            {
                PrintEnvelope(ResultEnvelope.FromFailure("Withdraw", "value.invalid", "Enter a valid id and amount."));
            }
            break;
        case "8":
            Console.Write("Account id: ");
            if (int.TryParse(Console.ReadLine(), out var closeId))
            {
                Print(await app.CloseAccountAsync(closeId));
            }
            else
            {
                PrintEnvelope(ResultEnvelope.FromFailure("Close", "id.invalid", "Enter a valid id."));
            }
            break;
        case "9":
            Print(await app.ListAccountsAsync());
            break;
        case "10":
            await app.RebuildReadModelsAsync();
            Console.WriteLine("Read models rebuilt.");
            break;
        case "0":
            return;
        default:
            Console.WriteLine(JsonSerializer.Serialize(
                ResultEnvelope.FromFailure("Menu", "menu.invalid", "Choose one of the listed options."),
                MixedPersistenceJson.Options));
            break;
    }
}

static void Print<TResult>(Result<TResult> result)
    where TResult : notnull =>
    Console.WriteLine(JsonSerializer.Serialize(ResultEnvelope.From(result), MixedPersistenceJson.Options));

static void PrintEnvelope(ResultEnvelope envelope) =>
    Console.WriteLine(JsonSerializer.Serialize(envelope, MixedPersistenceJson.Options));
