# Release screenshot checklist

Capture four real desktop screenshots at 1366×768 or larger using the isolated
**Command Palette LLM (Development)** Debug deployment. In Visual Studio,
select Debug and the machine architecture, then choose **Build → Deploy
CommandPaletteLLM** and reload Command Palette extensions.

The Visual Studio package has a separate identity from the Store release and
does not replace it or share its package data.

Use only fictional prompts, provider names, URLs, and responses. No API keys,
usernames, notifications, other applications, or personal files may be visible.

Use these filenames and scenes so Store and Gallery ordering remains stable:

1. `01-command-setup.png` — settings with a `Summarize` command editor.
2. `02-local-provider.png` — local provider at `http://127.0.0.1:8080/v1`, with
   the API-key field empty.
3. `03-command-invocation.png` — invoking `Summarize` with fictional text.
4. `04-structured-results.png` — several sanitized advanced-output results.

Crop to the Command Palette window while retaining enough Windows context to
show that the extension is genuine. Keep each Gallery copy below 1 MB. Review
every image at full resolution before submission.
