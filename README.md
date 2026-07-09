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
5. Click `Save Inbound`.
6. Use `Export Inbound` to create a JSON file compatible with 3x-ui's inbound import flow.
7. Use `Import Inbound` to import a JSON inbound export into the selected database.

## Compatibility Notes

- The app reads and writes the SQLite `inbounds` table directly.
- On save, `settings.clients` is synchronized into `client_traffics`, `clients`, and `client_inbounds` when those tables exist.
- Inbound export uses the same portable JSON shape accepted by 3x-ui's `/panel/api/inbounds/import` endpoint.
- Keep a copy of the original database before large edits. This tool edits the selected `.db` file in place.
