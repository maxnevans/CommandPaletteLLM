using System;
using System.Text.Json;

namespace CommandPaletteLLM;

internal static class CustomRequestArguments
{
    public static bool TryParse(
        string? json,
        out JsonDocument? document,
        out string? error)
    {
        document = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                document = null;
                error = "Custom request arguments must be a JSON object.";
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "model", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "messages", StringComparison.OrdinalIgnoreCase))
                {
                    var propertyName = property.Name;
                    document.Dispose();
                    document = null;
                    error = $"Custom request arguments cannot contain the protected '{propertyName}' field.";
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            error = "Custom request arguments must be valid JSON.";
            return false;
        }
    }
}
