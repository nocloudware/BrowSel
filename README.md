# BrowSel

Pick which browser and which profile opens a link.

Windows can only send a link to one program. BrowSel registers itself as the handler for
`http`/`https` links and, when you click one, it shows your browsers and their profiles — with
the ones already running marked — so you pick where the link goes.

Requires Windows 11.

![The BrowSel picker showing browser profiles and the Editor and Copy actions](BrowSel.jpg)

## Features

- **Browser + profile picker.** Every installed browser, every profile it has.
- **Already-open profiles are marked**, so you can tell at a glance what is running.
- **Ten languages.** English, Spanish, French, Russian, Chinese, Japanese, Portuguese, Hindi,
  Arabic and Bengali. It follows the operating system; you can override it in Settings.
- **Open in your editor.** Drops the link as text into the editor you pick, using the same
  editor Windows uses for text files.
- **Copy.** Puts the link on the clipboard and closes.
- **Check for updates** from inside the app.
- Light and dark, following the system.

## Build

Requires the .NET 10 SDK.

```
dotnet publish BrowSel.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o out
```

The program lands in `out\`. It carries the Windows App SDK with it, so it needs nothing else
installed. To build the installer as well, install [Inno Setup 6](https://jrsoftware.org/isdl.php)
and run:

```
"C:\Program Files\Inno Setup 6\ISCC.exe" installer\BrowSel.iss
```

That produces `dist\BrowSelSetup-<version>.exe`.

## Install

Download `BrowSelSetup-1.1.5.exe` from the releases page and run it. It installs into
`%LocalAppData%\Programs\BrowSel`, adds a Start menu entry and an entry in
*Apps installed by Windows*, and registers BrowSel as a link handler.

Then:

1. Open **Settings > Apps > Default apps** and find **BrowSel**.
2. Set it as the handler for both **HTTP** and **HTTPS**. Windows will not let any program choose
   this for you; you have to confirm it once. The installer offers to open that page for you.
3. Click any link on the web. BrowSel opens.

To remove it, uninstall it from *Apps installed by Windows*. That also removes your settings.
Running `BrowSel.exe --unregister` does the same from the command line, for a portable copy.

If it was still the default handler, Windows will ask you to pick another browser.

## Settings

The gear in the top right corner opens Settings:

- **Browsers to show** — only browsers actually installed are listed. Ticking and unticking
  updates the picker right away, so you can see the result before you confirm. Cancel puts
  everything back.
- **Open links in** — the editor used by the *Editor* option. It starts with whatever Windows
  uses for text files, falling back to Notepad.
- **Language** — overrides the system language.
- **Check for updates** — compares against the latest GitHub release and offers to install it.

## Keyboard

- `Esc` closes the window.

## How it works

Profiles come from each browser's own configuration: the `Local State` and profile directories
for Chromium-based browsers (Brave, Chrome, Edge, Opera), and `profiles.ini` for Firefox.

A profile counts as open when its lock file is held, when a running process points at it, or when
its window title matches it. Windows exposes no reliable API for this, so it is a best effort: if
a browser changes how it stores these, detection for that browser stops working.

The editor list is read from the same places Windows reads "Open with" from, so it matches what you
would see there.

## Limitations

- Windows 11 only.
- Open-profile detection is a heuristic, not official API. It can miss a profile, or report one as
  open after it closed.
- Auto-update needs a GitHub release to exist. With none published, it always reports that you
  are up to date.

## License

MIT. See [LICENSE](LICENSE). Third-party components are listed in
[THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).