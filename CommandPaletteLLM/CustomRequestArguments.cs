using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommandPaletteLLM;

internal static class CustomRequestArguments
{
    public static bool TryParse(
        string? json,
        out JsonDocument? document,
        out string? error,
        string description = "Custom request arguments")
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
                error = $"{description} must be a JSON object.";
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
                    error = $"{description} cannot contain the protected '{propertyName}' field.";
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            error = $"{description} must be valid JSON.";
            return false;
        }
    }

    public static bool TryMerge(
        string? templateJson,
        string? commandJson,
        out JsonDocument? document,
        out string? error)
    {
        document = null;
        if (!TryParse(
            templateJson,
            out var templateDocument,
            out error,
            "Template request arguments"))
        {
            return false;
        }

        if (!TryParse(commandJson, out var commandDocument, out error))
        {
            templateDocument?.Dispose();
            return false;
        }

        using (templateDocument)
        using (commandDocument)
        {
            if (templateDocument is null && commandDocument is null)
            {
                return true;
            }

            var merged = templateDocument is null
                ? new JsonObject()
                : JsonNode.Parse(templateDocument.RootElement.GetRawText())!.AsObject();
            if (commandDocument is not null)
            {
                var command = JsonNode.Parse(commandDocument.RootElement.GetRawText())!.AsObject();
                MergeObjects(merged, command);
            }

            document = JsonDocument.Parse(merged.ToJsonString());
            error = null;
            return true;
        }
    }

    private static void MergeObjects(JsonObject target, JsonObject overrides)
    {
        foreach (var property in overrides)
        {
            if (property.Value is null && target.ContainsKey(property.Key))
            {
                target.Remove(property.Key);
                continue;
            }

            if (property.Value is JsonObject overrideObject &&
                target[property.Key] is JsonObject targetObject)
            {
                MergeObjects(targetObject, overrideObject);
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }
}
