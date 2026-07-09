# XUI DbManager

Portable Windows editor for 3x-ui / x-ui SQLite databases.

## Run

Use the self-contained build:

```text
publish\win-x64-portable\XUI_DbManager.exe
```

## Open in Visual Studio

Open the solution file:

```text
XUI_DbManager.slnx
```

## Workflow

1. Click `Open DB` and select one or more `x-ui.db` files.
2. Each database opens in its own tab.
3. Select an inbound on the left.
4. Edit inbound fields in `Inbound`, clients in `Clients`, or raw `settings`, `streamSettings`, and `sniffing` in `JSON`.
5. Press Enter in edited fields or leave the cell to save automatically.
6. Open `All Clients` to search every client field, edit user-friendly traffic/expiry values, and move a selected client to another inbound.
7. Use `Export Inbound` to create a JSON file compatible with 3x-ui's inbound import flow.
8. Use `Import Inbound` to import a JSON inbound export into the selected database.

## GitHub Release

Tag a SemVer version with a `v` prefix and push it:

```text
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will create a release with these Windows x64 assets:

```text
XUI_DbManager_Portable_v1.0.0_win-x64.zip
XUI_DbManager_Setup_v1.0.0_win-x64.exe
```

The installer is built with Inno Setup and installs per-user under `%LOCALAPPDATA%\Programs\XUI DB Manager`, so it does not require administrator privileges.

## Compatibility Notes

- The app reads and writes the SQLite `inbounds` table directly.
- On save, `settings.clients` is synchronized into `client_traffics`, `clients`, and `client_inbounds` when those tables exist.
- Inbound export uses the same portable JSON shape accepted by 3x-ui's inbound import flow.
- Keep a copy of the original database before large edits. This tool edits the selected `.db` file in place.


