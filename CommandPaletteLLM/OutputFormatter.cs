using System.Text;

namespace CommandPaletteLLM;

internal static class OutputFormatter
{
    public static bool TryFormat(string format, string argument, out string result, out string error)
    {
        var output = new StringBuilder(format.Length + argument.Length);

        for (var index = 0; index < format.Length; index++)
        {
            var current = format[index];
            if (current == '{')
            {
                if (index + 1 >= format.Length)
                {
                    return Fail("An opening brace must be followed by either '{' or '}'.", out result, out error);
                }

                var next = format[index + 1];
                if (next == '{')
                {
                    output.Append('{');
                    index++;
                    continue;
                }

                if (next == '}')
                {
                    output.Append(argument);
                    index++;
                    continue;
                }

                return Fail("Only {} replacement fields and escaped {{ braces are supported.", out result, out error);
            }

            if (current == '}')
            {
                if (index + 1 < format.Length && format[index + 1] == '}')
                {
                    output.Append('}');
                    index++;
                    continue;
                }

                return Fail("A closing brace must be escaped as }}.", out result, out error);
            }

            output.Append(current);
        }

        result = output.ToString();
        error = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string result, out string error)
    {
        result = string.Empty;
        error = message;
        return false;
    }
}
