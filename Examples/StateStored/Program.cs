using System.Text.Json;
using GaWeCodes.Thessera.Application.Results;
using StateStored;

var connectionString = Environment.GetEnvironmentVariable("THESSERA_EXAMPLE_POSTGRES")
    ?? StateStoredApplication.DefaultConnectionString;

await using var app = await StateStoredApplication.StartAsync(connectionString);

while (true)
{
    Console.WriteLine();
    Console.WriteLine("1) Create");
    Console.WriteLine("2) List");
    Console.WriteLine("3) Update");
    Console.WriteLine("4) Delete");
    Console.WriteLine("0) Exit");
    Console.Write("Select: ");

    var selection = Console.ReadLine();
    switch (selection)
    {
        case "1":
            Console.Write("Value: ");
            if (int.TryParse(Console.ReadLine(), out var createValue))
            {
                Print(await app.CreateAsync(createValue));
            }
            else
            {
                PrintEnvelope(ResultEnvelope.FromFailure("Create", "value.invalid", "Enter a whole number."));
            }
            break;
        case "2":
            Print(await app.ListAsync());
            break;
        case "3":
            Console.Write("Reading id: ");
            var updateIdText = Console.ReadLine();
            Console.Write("New value: ");
            var updateValueText = Console.ReadLine();
            if (int.TryParse(updateIdText, out var updateId) && int.TryParse(updateValueText, out var updateValue))
            {
                Print(await app.UpdateAsync(updateId, updateValue));
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
                Print(await app.DeleteAsync(deleteId));
            }
            else
            {
                PrintEnvelope(ResultEnvelope.FromFailure("Delete", "id.invalid", "Enter a valid id."));
            }
            break;
        case "0":
            return;
        default:
            Console.WriteLine(JsonSerializer.Serialize(
                ResultEnvelope.FromFailure("Menu", "menu.invalid", "Choose one of the listed options."),
                StateStoredJson.Options));
            break;
    }
}

static void Print<TResult>(Result<TResult> result)
    where TResult : notnull =>
    Console.WriteLine(JsonSerializer.Serialize(ResultEnvelope.From(result), StateStoredJson.Options));

static void PrintEnvelope(ResultEnvelope envelope) =>
    Console.WriteLine(JsonSerializer.Serialize(envelope, StateStoredJson.Options));
