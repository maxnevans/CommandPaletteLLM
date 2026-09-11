using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CommandPaletteLLM;

internal sealed partial class StableCopyTextCommand : InvokableCommand
{
    private string _text = string.Empty;

    public StableCopyTextCommand(string id)
    {
        Id = id;
        Name = string.Empty;
        Icon = new IconInfo("\uE8C8");
    }

    internal string Text => _text;

    internal void SetText(string? text) => _text = text ?? string.Empty;

    public override ICommandResult Invoke() =>
        string.IsNullOrEmpty(_text)
            ? CommandResult.KeepOpen()
            : new CopyTextCommand(_text).Invoke();
}
