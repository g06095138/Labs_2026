using System.Text.Json;
using System.Text.Json.Serialization;
using TelecomFinance.Domain;
namespace TelecomFinance.Console;

internal static class PlanInput
{
    private sealed record PlanDto(Guid Id, string Name, decimal InitialCash,
        int StartYear, int StartMonth, MonthParameters[] Months);
    public static FinancialPlan Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        RejectDuplicateKeys(document.RootElement, "Input");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true
        };
        var dto = document.RootElement.Deserialize<PlanDto>(options)
            ?? throw new FinanceValidationException("Input", "Ожидается объект плана.");
        return new(dto.Id, dto.Name, dto.InitialCash, dto.StartYear, dto.StartMonth, dto.Months);
    }
    private static void RejectDuplicateKeys(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new FinanceValidationException(path + "." + property.Name, "Повторное имя поля JSON.");
                RejectDuplicateKeys(property.Value, path + "." + property.Name);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var item in element.EnumerateArray()) RejectDuplicateKeys(item, $"{path}[{i++}]");
        }
    }
}
