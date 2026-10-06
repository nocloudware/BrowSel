# BrowSel

Pick which browser and profile opens a link, without changing your system
default browser.

BrowSel registers itself as the handler for `http` and `https` links. When you
click a link, a window lists every installed profile; the ones already open are
marked in green. Pick one and the link opens there.

## Features

- **Finds the browsers you have installed**: Brave, Google Chrome, Microsoft
  Edge, Opera and Firefox. Nothing to configure by hand.
- **Knows which profiles are open** and marks them with a green dot, so you
  never open one blindly.
  - For Chromium browsers it cross-checks four signals: the command line of the
    live processes, the window titles, the profile lock files, and as a last
    resort the list of recently used profiles.
  - For Firefox it reads `profiles.ini` and checks the `parent.lock` file.
- **Opens the right profile**: Chromium gets `--profile-directory=`, Firefox
  gets `-P`. If the profile was already open the link is added as a new tab
  instead of opening another window.
- **Nothing to install or run as a service**: a single executable that
  registers itself under your own Windows user. It never asks for administrator
  rights.
- **Native Windows 11 look**: WinUI 3 interface, Mica backdrop, system fonts
  and colors, and it follows the light or dark theme automatically.
- **Settings**: the gear at the top lets you choose which browsers appear in the
  list. Your choice is remembered.
- **Remembers how you left it**: which browsers are ticked and which browsers
  were expanded or collapsed.
- **No argument injection**: the address is handed straight to the browser
  process, never through a command line.

## Requirements

- Windows 10 version 1809 (17763) or later. Developed and tested on Windows 11.
- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build
  it. The app ships everything needed to run, so there is nothing to install
  afterwards.

## Build

```powershell
git clone https://github.com/nocloudware/BrowSel.git
cd BrowSel
dotnet build BrowSel.csproj -c Release -p:Platform=x64
```

The output lands in `bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\`.

## Install

```powershell
.\BrowSel.exe --register
```

That registers the link handler under your Windows user. Windows then shows you
a message: **registered is not the same as being the default**. You have to
confirm it yourself:

1. Open **Settings → Apps → Default apps**.
2. Look for **BrowSel**.
3. Set it for **HTTP** and for **HTTPS**.

Done. The next time you click a link, BrowSel opens.

> Note: the `bin\...` folder *is* the application. **Do not delete or move it**,
> because the Windows registry points at the executable inside it.

## Uninstall

```powershell
.\BrowSel.exe --unregister
```

This removes the protocol from the registry. Then take BrowSel off the *Default
apps* list and you can delete the folder.

> `--unregister` also deletes your saved preferences.

## Keyboard shortcuts

| Action | How |
| --- | --- |
| Expand or collapse a browser | Click its name |
| Open the link in a profile | Click the profile |
| Close without opening anything | `Esc` |

## How it works

- **Discovery**: browsers are looked up in the standard locations (Program
  Files, Program Files (x86), `%LOCALAPPDATA%`) and, if they are not there, the
  Windows registry is consulted.
- **Profiles**: Chromium browsers keep their profiles under `User Data`, and the
  names come from the `Local State` file. Firefox uses its `profiles.ini`.
- **Open state**: Chromium browsers leave a lock file per profile while they
  hold it open. BrowSel tries to open those files exclusively; if it cannot, they
  are in use.
- **Registration**: everything is written under
  `HKEY_CURRENT_USER\Software\BrowSel` and
  `HKEY_CURRENT_USER\Software\Classes\BrowSelURL`. Nothing system-wide is
  touched.

## Limitations

- Because of how Windows works, you cannot leave a single native browser+profile
  for the whole system: BrowSel always shows its window so you can choose.
- Chromium open-profile detection relies on the browser's lock files. If a
  future version changes them, detection may stop working and every profile will
  show as closed (the link still opens fine).
- Windows does not let an application make itself the default: step 3 of the
  install always has to be done by hand.

## License

[MIT](LICENSE). See [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt) for the
third-party components.