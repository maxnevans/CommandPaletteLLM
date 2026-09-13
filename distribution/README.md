# Publication handoff

This directory contains metadata that is copied into external publication
systems after the corresponding installation source is live.

- `STORE-LISTING-en-US.md` contains the Partner Center fields, certification
  notes, and screenshot captions.
- [Build a Microsoft Store submission](../README.md#build-a-microsoft-store-submission)
  documents the Visual Studio 2026 **Pack** workflow and identifies which
  generated `.msixupload` files belong in Partner Center.
- After Microsoft Store certification, create the Gallery `extension.json`
  using the assigned Store product ID, then copy it with `gallery/icon.png` and
  the approved screenshots to
  `extensions/maxnevans/command-palette-llm/` in `microsoft/CmdPal-Extensions`.

Do not submit Gallery metadata before at least one listed source is publicly
installable. Do not commit Store account credentials, tokens, certificates, or
identity-verification material.
