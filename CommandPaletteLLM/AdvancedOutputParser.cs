using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CommandPaletteLLM;

internal static class AdvancedOutputParser
{
    public static bool TryParse(string response, out AdvancedOutput output)
    {
        output = new AdvancedOutput(string.Empty, string.Empty, string.Empty, string.Empty, []);

        try
        {
            var json = UnwrapCodeFence(response);
            if (json is null)
            {
                return false;
            }

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !TryReadString(document.RootElement, "title", out var title) ||
                !TryReadString(document.RootElement, "subtitle", out var subtitle) ||
                !TryReadString(document.RootElement, "details", out var details) ||
                !TryReadString(document.RootElement, "section", out var section) ||
                !TryReadTags(document.RootElement, out var tags))
            {
                return false;
            }

            output = new AdvancedOutput(title, subtitle, details, section, tags);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? UnwrapCodeFence(string response)
    {
        var value = response.Trim();
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        var firstLineEnd = value.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return null;
        }

        var openingFence = value[..firstLineEnd].TrimEnd('\r');
        if (!string.Equals(openingFence, "```", StringComparison.Ordinal) &&
            !string.Equals(openingFence, "```json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        const string closingFence = "```";
        if (!value.EndsWith(closingFence, StringComparison.Ordinal))
        {
            return null;
        }

        return value.Substring(
            firstLineEnd + 1,
            value.Length - firstLineEnd - 1 - closingFence.Length).Trim();
    }

    private static bool TryReadString(
        JsonElement root,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryReadTags(JsonElement root, out IReadOnlyList<string> tags)
    {
        tags = [];
        if (!root.TryGetProperty("tags", out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsedTags = new List<string>();
        foreach (var tag in property.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            parsedTags.Add(tag.GetString() ?? string.Empty);
        }

        tags = parsedTags;
        return true;
    }
}
