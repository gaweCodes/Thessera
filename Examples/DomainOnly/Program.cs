using System.Text.Json;
using DomainOnly;

var store = new ReadingStore();

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
            Print(int.TryParse(Console.ReadLine(), out var createValue)
                ? store.Create(createValue)
                : OperationResult.Failure("Create", "Enter a whole number."));
            break;
        case "2":
            Print(store.List());
            break;
        case "3":
            Console.Write("Reading id: ");
            var updateIdText = Console.ReadLine();
            Console.Write("New value: ");
            var updateValueText = Console.ReadLine();
            Print(int.TryParse(updateIdText, out var updateId) && int.TryParse(updateValueText, out var updateValue)
                ? store.Update(updateId, updateValue)
                : OperationResult.Failure("Update", "Enter a valid id and whole number."));
            break;
        case "4":
            Console.Write("Reading id: ");
            Print(int.TryParse(Console.ReadLine(), out var deleteId)
                ? store.Delete(deleteId)
                : OperationResult.Failure("Delete", "Enter a valid id."));
            break;
        case "0":
            return;
        default:
            Print(OperationResult.Failure("Menu", "Choose one of the listed options."));
            break;
    }
}

static void Print(OperationResult result) =>
    Console.WriteLine(JsonSerializer.Serialize(result, DomainOnlyJson.Options));
