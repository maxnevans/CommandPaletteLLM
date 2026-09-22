using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CommandPaletteLLM;

internal static class AdvancedOutputParser
{
    public static bool TryParse(
        string response,
        out IReadOnlyList<AdvancedOutputEntry> entries)
    {
        entries = [];

        try
        {
            var json = UnwrapCodeFence(response);
            if (json is null)
            {
                return false;
            }

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (!TryParseObject(document.RootElement, out var output, out _))
                {
                    return false;
                }

                entries = [AdvancedOutputEntry.Valid(output)];
                return true;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var parsedEntries = new List<AdvancedOutputEntry>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                parsedEntries.Add(TryParseObject(element, out var output, out var errorTitle)
                    ? AdvancedOutputEntry.Valid(output)
                    : AdvancedOutputEntry.Invalid(errorTitle, element.GetRawText()));
            }

            entries = parsedEntries;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseObject(
        JsonElement root,
        out AdvancedOutput output,
        out string errorTitle)
    {
        output = new AdvancedOutput(string.Empty, string.Empty, string.Empty, string.Empty, []);
        errorTitle = string.Empty;

        if (root.ValueKind != JsonValueKind.Object)
        {
            errorTitle = "Invalid variation: expected an object";
            return false;
        }

        if (!TryReadString(root, "title", out var title, out errorTitle) ||
            !TryReadString(root, "subtitle", out var subtitle, out errorTitle) ||
            !TryReadString(root, "details", out var details, out errorTitle) ||
            !TryReadString(root, "section", out var section, out errorTitle) ||
            !TryReadTags(root, out var tags, out errorTitle))
        {
            return false;
        }

        output = new AdvancedOutput(title, subtitle, details, section, tags);
        return true;
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
        out string value,
        out string errorTitle)
    {
        value = string.Empty;
        errorTitle = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            errorTitle = $"Invalid variation: \"{propertyName}\" must be a string";
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryReadTags(
        JsonElement root,
        out IReadOnlyList<string> tags,
        out string errorTitle)
    {
        tags = [];
        errorTitle = string.Empty;
        if (!root.TryGetProperty("tags", out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            errorTitle = "Invalid variation: \"tags\" must be an array";
            return false;
        }

        var parsedTags = new List<string>();
        foreach (var tag in property.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.String)
            {
                errorTitle = "Invalid variation: \"tags\" must contain only strings";
                return false;
            }

            parsedTags.Add(tag.GetString() ?? string.Empty);
        }

        tags = parsedTags;
        return true;
    }
}
