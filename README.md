# BrowSel

Pick which browser and which profile opens a link.

Windows can only send a link to one program. BrowSel registers itself as the handler for
`http`/`https` links and, when you click one, it shows your browsers and their profiles — with
the ones already running marked — so you pick where the link goes.

Requires Windows 10 2004+ (19041) or Windows 11. On Windows 10, Mica backdrop falls back to solid color.


<p align="center">
  <br>BrowSel picker<br><img src="docs/BrowSel-main.png" width="33%" alt="The BrowSel picker showing browser profiles and the Editor and Copy actions">
  <br>Expanded profile list<br>
  <img src="docs/BrowSel-expanded.jpg" width="33%" alt="Brave's expanded profile list in BrowSel">
  <br>Settings dialog<br>
  <img src="docs/BrowSel-config.png" width="33%" alt="BrowSel's Settings dialog with browser selection and language options">
  <br>About dialog<br>
  <img src="docs/BrowSel-about.png" width="33%" alt="BrowSel's About dialog showing the app icon, version, and credits">
</p>

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

Requires the .NET 10 SDK and [Inno Setup 6](https://jrsoftware.org/isdl.php).

```
./build.ps1
```

The version lives only in `<Version>` of `BrowSel.csproj`. The script publishes the app, compiles
the installer with that version and writes `dist\BrowSelSetup-<version>.exe` plus its
`BrowSelSetup-<version>.exe.sha256` (the in-app updater refuses a release without that hash).
Building manually? Use:

```
dotnet publish BrowSel.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o out-rel
```

and point ISCC (usually under `C:\Program Files (x86)\Inno Setup 6\`) at `installer\BrowSel.iss`.

To publish a release: bump `<Version>` in `BrowSel.csproj`, commit, then
`git tag vX.Y.Z && git push --tags` — the release workflow builds and uploads the installer and
its hash.

## Install

Download the latest `BrowSelSetup-*.exe` from the releases page and run it. It installs into
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
for Chromium-based browsers (Brave, Chrome, Edge, Opera, Vivaldi), and `profiles.ini` for Firefox.

A profile counts as open when its lock file is held, when a running process points at it, or when
its window title matches it. Windows exposes no reliable API for this, so it is a best effort: if
a browser changes how it stores these, detection for that browser stops working.

The editor list is read from the same places Windows reads "Open with" from, so it matches what you
would see there.

## Limitations

- Windows 10 2004+ (19041) or Windows 11. On Windows 10, Mica backdrop falls back to solid color.
- Open-profile detection is a heuristic, not official API. It can miss a profile, or report one as
  open after it closed.
- Auto-update needs a GitHub release carrying the installer **and** its hash
  (`BrowSelSetup-*.exe.sha256` or GitHub's `digest`). With no hash published, or with none of the
  two files, it always reports that you are up to date.

## License

MIT. See [LICENSE](LICENSE). Third-party components are listed in
[THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).
