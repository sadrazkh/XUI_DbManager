using System.ComponentModel;
using System.Text.Json.Nodes;

namespace XuiDbManager;

public sealed class MainForm : Form
{
    private static readonly Color AppBack = Color.FromArgb(245, 247, 250);
    private static readonly Color PanelBack = Color.White;
    private static readonly Color BorderColor = Color.FromArgb(226, 232, 240);
    private static readonly Color Primary = Color.FromArgb(25, 118, 210);
    private static readonly Color PrimaryDark = Color.FromArgb(13, 71, 161);
    private static readonly Color TextMain = Color.FromArgb(31, 41, 55);
    private static readonly Color TextMuted = Color.FromArgb(100, 116, 139);

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ToolStrip _tools = new() { GripStyle = ToolStripGripStyle.Hidden };
    private readonly ToolStripButton _openButton = new("Open DB");
    private readonly ToolStripButton _reloadButton = new("Reload");
    private readonly ToolStripButton _saveButton = new("Save Inbound");
    private readonly ToolStripButton _exportButton = new("Export Inbound");
    private readonly ToolStripButton _importButton = new("Import Inbound");
    private readonly ToolStripLabel _status = new("Ready") { Alignment = ToolStripItemAlignment.Right, TextAlign = ContentAlignment.MiddleRight };
    private readonly System.Windows.Forms.Timer _autoSaveTimer = new() { Interval = 450 };

    private readonly Dictionary<TabPage, DatabaseDocument> _documents = [];
    private string _pendingAutoSaveMessage = "Saved";
    private bool _isSaving;
    private bool _saveAgainAfterCurrent;

    public MainForm()
    {
        Text = "3x-ui SQLite DB Manager";
        Width = 1420;
        Height = 850;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppBack;
        Font = new Font("Segoe UI", 9.5f);

        StyleToolStrip();
        _tools.Items.AddRange([_openButton, _reloadButton, new ToolStripSeparator(), _saveButton, _exportButton, _importButton, _status]);
        Controls.Add(_tabs);
        Controls.Add(_tools);
        _tools.Dock = DockStyle.Top;
        _tabs.Appearance = TabAppearance.FlatButtons;
        _tabs.Padding = new Point(14, 5);

        _openButton.Click += (_, _) => OpenDatabases();
        _reloadButton.Click += (_, _) => ReloadCurrent();
        _saveButton.Click += (_, _) => SaveCurrentInbound();
        _exportButton.Click += (_, _) => ExportCurrentInbound();
        _importButton.Click += (_, _) => ImportInboundIntoCurrentDb();
        _autoSaveTimer.Tick += (_, _) =>
        {
            _autoSaveTimer.Stop();
            SaveCurrentInbound(_pendingAutoSaveMessage);
        };

        FormClosed += (_, _) =>
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Dispose();
            foreach (var doc in _documents.Values)
                doc.Repository.Dispose();
        };
    }

    private void StyleToolStrip()
    {
        _tools.BackColor = PrimaryDark;
        _tools.ForeColor = Color.White;
        _tools.Padding = new Padding(8, 6, 8, 6);
        foreach (var button in new[] { _openButton, _reloadButton, _saveButton, _exportButton, _importButton })
        {
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.ForeColor = Color.White;
            button.BackColor = PrimaryDark;
            button.Margin = new Padding(3, 0, 3, 0);
            button.Padding = new Padding(10, 4, 10, 4);
        }
        _status.ForeColor = Color.FromArgb(220, 237, 255);
    }

    private void OpenDatabases()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open 3x-ui SQLite database",
            Filter = "SQLite DB (*.db;*.sqlite;*.sqlite3)|*.db;*.sqlite;*.sqlite3|All files (*.*)|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in dialog.FileNames)
            OpenDatabase(path);
    }

    private void OpenDatabase(string path)
    {
        try
        {
            var repo = new XuiRepository(path);
            var doc = new DatabaseDocument(path, repo);
            LoadDocument(doc);

            var page = new TabPage(Path.GetFileName(path)) { ToolTipText = path };
            page.Controls.Add(CreateDatabaseView(doc));
            _tabs.TabPages.Add(page);
            _documents[page] = doc;
            _tabs.SelectedTab = page;
            SetStatus($"Opened {path}");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private Control CreateDatabaseView(DatabaseDocument doc)
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 620, BackColor = BorderColor };
        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = AppBack,
            Padding = new Padding(12)
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Controls.Add(new Label
        {
            Text = $"{Path.GetFileName(doc.Path)}    Inbounds: {doc.Inbounds.Count}",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 11),
            ForeColor = TextMain,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var inboundGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            DataSource = doc.Inbounds,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RowHeadersVisible = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            AllowUserToResizeRows = false,
            GridColor = BorderColor,
            ColumnHeadersHeight = 38,
            EnableHeadersVisualStyles = false
        };
        inboundGrid.RowTemplate.Height = 34;
        inboundGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255);
        inboundGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
        inboundGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
        inboundGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(43, 108, 176);
        inboundGrid.DefaultCellStyle.SelectionForeColor = Color.White;
        inboundGrid.Columns.Add(CheckColumn(nameof(InboundRow.Enable), "On", 46));
        inboundGrid.Columns.Add(TextColumn(nameof(InboundRow.Id), "ID", 52, true));
        inboundGrid.Columns.Add(TextColumn(nameof(InboundRow.Remark), "Remark", 210));
        inboundGrid.Columns.Add(TextColumn(nameof(InboundRow.Protocol), "Protocol", 82));
        inboundGrid.Columns.Add(TextColumn(nameof(InboundRow.Port), "Port", 62));
        inboundGrid.Columns.Add(TextColumn(nameof(InboundRow.Listen), "Listen", 110));
        inboundGrid.Columns.Add(TextColumn(nameof(InboundRow.Tag), "Tag", 180));
        inboundGrid.Columns.Add(TextColumn(nameof(InboundRow.Total), "Total", 90));
        inboundGrid.Columns.Add(TextColumn(nameof(InboundRow.ExpiryTime), "Expiry", 115));
        inboundGrid.SelectionChanged += (_, _) =>
        {
            if (inboundGrid.CurrentRow?.DataBoundItem is InboundRow inbound)
                SelectInbound(doc, inbound, split.Panel2);
        };
        left.Controls.Add(inboundGrid, 0, 1);
        split.Panel1.Controls.Add(left);

        if (doc.Inbounds.Count > 0)
        {
            var selected = doc.CurrentInbound ?? doc.Inbounds[0];
            var rowIndex = doc.Inbounds.IndexOf(selected);
            if (rowIndex < 0)
                rowIndex = 0;
            if (inboundGrid.Rows.Count > rowIndex)
            {
                inboundGrid.ClearSelection();
                inboundGrid.Rows[rowIndex].Selected = true;
                inboundGrid.CurrentCell = inboundGrid.Rows[rowIndex].Cells[0];
            }
            SelectInbound(doc, doc.Inbounds[rowIndex], split.Panel2);
        }
        else
        {
            split.Panel2.Controls.Add(new Label
            {
                Text = "No inbounds found in this database.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.DimGray
            });
        }

        return split;
    }

    private void SelectInbound(DatabaseDocument doc, InboundRow inbound, Control detailPanel)
    {
        doc.CurrentInbound = inbound;
        doc.CurrentClients.Clear();

        try
        {
            var settings = JsonUtil.ParseObjectOrEmpty(inbound.Settings);
            foreach (var client in JsonUtil.GetClientsArray(settings).OfType<JsonObject>().Select(JsonUtil.ClientFromNode))
                doc.CurrentClients.Add(client);
        }
        catch
        {
            // Keep the JSON editor usable even if settings is broken.
        }

        detailPanel.Controls.Clear();
        detailPanel.Controls.Add(CreateInboundDetail(doc, inbound));
    }

    private Control CreateInboundDetail(DatabaseDocument doc, InboundRow inbound)
    {
        var detailTabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 5) };
        var source = new BindingSource { DataSource = inbound };
        detailTabs.Tag = source;
        detailTabs.TabPages.Add(new TabPage("Inbound") { Controls = { CreateInboundEditor(source, inbound) } });

        var clientGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            DataSource = doc.CurrentClients,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            AllowUserToResizeRows = false,
            GridColor = BorderColor,
            ColumnHeadersHeight = 38,
            EnableHeadersVisualStyles = false
        };
        var clientGridReady = false;
        clientGrid.RowTemplate.Height = 34;
        clientGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255);
        clientGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
        clientGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
        clientGrid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            SetStatus($"Client cell value is invalid: {e.Exception?.Message}");
        };
        clientGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (clientGrid.IsCurrentCellDirty)
                clientGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        clientGrid.CellEndEdit += (_, _) =>
        {
            if (clientGridReady)
                ScheduleAutoSave("Client saved");
        };
        clientGrid.CellValueChanged += (_, _) =>
        {
            if (clientGridReady)
                ScheduleAutoSave("Client saved");
        };
        clientGrid.UserDeletedRow += (_, _) =>
        {
            if (clientGridReady)
                ScheduleAutoSave("Client removed");
        };
        clientGrid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                ScheduleAutoSave("Client saved");
            }
        };
        clientGrid.Columns.Add(CheckColumn(nameof(ClientView.Enable), "On", 46));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.Email), "Email", 160));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.Id), "UUID/ID", 250));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.Password), "Password", 140));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.Auth), "Auth", 120));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.Flow), "Flow", 150));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.Security), "Security", 90));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.SubId), "Sub ID", 150));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.TotalGB), "Traffic Bytes", 120));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.ExpiryTime), "Expiry", 130));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.LimitIp), "IP Limit", 80));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.Comment), "Comment", 180));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.Group), "Group", 110));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.PublicKey), "WG Public", 180));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.AllowedIPs), "WG Allowed IPs", 180));
        clientGrid.Columns.Add(TextColumn(nameof(ClientView.Secret), "MTProto Secret", 180));
        detailTabs.TabPages.Add(new TabPage("Clients") { Controls = { clientGrid } });
        clientGridReady = true;

        var jsonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(8)
        };
        jsonPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        jsonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        jsonPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        jsonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        jsonPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        jsonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

        var settingsBox = JsonBox(inbound.Settings);
        var streamBox = JsonBox(inbound.StreamSettings);
        var sniffingBox = JsonBox(inbound.Sniffing);
        settingsBox.Tag = "settings";
        streamBox.Tag = "stream";
        sniffingBox.Tag = "sniffing";
        jsonPanel.Controls.Add(new Label { Text = "settings", Dock = DockStyle.Fill }, 0, 0);
        jsonPanel.Controls.Add(settingsBox, 0, 1);
        jsonPanel.Controls.Add(new Label { Text = "streamSettings", Dock = DockStyle.Fill }, 0, 2);
        jsonPanel.Controls.Add(streamBox, 0, 3);
        jsonPanel.Controls.Add(new Label { Text = "sniffing", Dock = DockStyle.Fill }, 0, 4);
        jsonPanel.Controls.Add(sniffingBox, 0, 5);
        detailTabs.TabPages.Add(new TabPage("JSON") { Controls = { jsonPanel } });

        return detailTabs;
    }

    private Control CreateInboundEditor(BindingSource source, InboundRow inbound)
    {
        var outer = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PanelBack, Padding = new Padding(14) };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = PanelBack
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        AddTextRow(grid, source, "Remark", nameof(InboundRow.Remark), "Tag", nameof(InboundRow.Tag));
        AddComboRow(grid, source, "Protocol", nameof(InboundRow.Protocol), ["vmess", "vless", "trojan", "shadowsocks", "wireguard", "hysteria", "http", "mixed", "tunnel", "mtproto"], "Traffic Reset", nameof(InboundRow.TrafficReset), ["never", "hourly", "daily", "weekly", "monthly"]);
        AddTextRow(grid, source, "Port", nameof(InboundRow.Port), "Listen", nameof(InboundRow.Listen));
        AddTextRow(grid, source, "Total Bytes", nameof(InboundRow.Total), "Expiry Time", nameof(InboundRow.ExpiryTime));
        AddTextRow(grid, source, "Upload", nameof(InboundRow.Up), "Download", nameof(InboundRow.Down));
        AddTextRow(grid, source, "Sub Sort", nameof(InboundRow.SubSortIndex), "Last Reset", nameof(InboundRow.LastTrafficResetTime));
        AddComboRow(grid, source, "Share Mode", nameof(InboundRow.ShareAddrStrategy), ["node", "listen", "custom"], "Share Host", nameof(InboundRow.ShareAddr), null);

        var enable = new CheckBox { Text = "Inbound enabled", Checked = inbound.Enable, AutoSize = true, Margin = new Padding(3, 10, 3, 10) };
        enable.DataBindings.Add("Checked", source, nameof(InboundRow.Enable), false, DataSourceUpdateMode.OnPropertyChanged);
        grid.Controls.Add(enable, 1, grid.RowCount);
        grid.SetColumnSpan(enable, 3);
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        grid.RowCount++;

        outer.Controls.Add(grid);
        WireEnterAutoSave(outer);
        return outer;
    }

    private static TextBox JsonBox(string text)
    {
        var box = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 10),
            Text = PrettyOrOriginal(text)
        };
        box.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (box.FindForm() is MainForm form)
                    form.SaveCurrentInbound("JSON saved");
            }
        };
        return box;
    }

    private static void AddTextRow(TableLayoutPanel grid, BindingSource source, string leftLabel, string leftProperty, string rightLabel, string rightProperty)
    {
        var row = grid.RowCount;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        grid.Controls.Add(EditorLabel(leftLabel), 0, row);
        grid.Controls.Add(BoundText(source, leftProperty), 1, row);
        grid.Controls.Add(EditorLabel(rightLabel), 2, row);
        grid.Controls.Add(BoundText(source, rightProperty), 3, row);
        grid.RowCount++;
    }

    private static void AddComboRow(
        TableLayoutPanel grid,
        BindingSource source,
        string leftLabel,
        string leftProperty,
        string[] leftItems,
        string rightLabel,
        string rightProperty,
        string[]? rightItems)
    {
        var row = grid.RowCount;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        grid.Controls.Add(EditorLabel(leftLabel), 0, row);
        grid.Controls.Add(BoundCombo(source, leftProperty, leftItems), 1, row);
        grid.Controls.Add(EditorLabel(rightLabel), 2, row);
        grid.Controls.Add(rightItems is null ? BoundText(source, rightProperty) : BoundCombo(source, rightProperty, rightItems), 3, row);
        grid.RowCount++;
    }

    private static Label EditorLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(55, 65, 81),
            Margin = new Padding(0, 4, 8, 4)
        };
    }

    private static TextBox BoundText(BindingSource source, string property)
    {
        var box = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 16, 6),
            BorderStyle = BorderStyle.FixedSingle
        };
        box.DataBindings.Add("Text", source, property, true, DataSourceUpdateMode.OnValidation);
        return box;
    }

    private static ComboBox BoundCombo(BindingSource source, string property, string[] items)
    {
        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            Margin = new Padding(0, 6, 16, 6)
        };
        combo.Items.AddRange(items);
        combo.DataBindings.Add("Text", source, property, true, DataSourceUpdateMode.OnPropertyChanged);
        combo.SelectionChangeCommitted += (_, _) =>
        {
            if (combo.FindForm() is MainForm form)
                form.ScheduleAutoSave("Inbound saved");
        };
        return combo;
    }

    private void WireEnterAutoSave(Control root)
    {
        foreach (var control in Descendants<Control>(root).Append(root))
        {
            if (control is TextBox { Multiline: false } textBox)
            {
                textBox.KeyDown += (_, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        ScheduleAutoSave("Inbound saved");
                    }
                };
                textBox.Leave += (_, _) => CommitBindingsOnly();
            }
        }
    }

    private static string PrettyOrOriginal(string text)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            return JsonUtil.NormalizeObjectJson(text);
        }
        catch
        {
            return text;
        }
    }

    private void SaveCurrentInbound()
    {
        SaveCurrentInbound("Saved inbound");
    }

    private void SaveCurrentInbound(string message)
    {
        if (SaveCurrentInboundFromUi())
            SetStatus(message);
    }

    private void AutoSaveCurrentInbound(string message) => ScheduleAutoSave(message);

    private void ScheduleAutoSave(string message)
    {
        _pendingAutoSaveMessage = message;
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
        SetStatus("Pending save...");
    }

    private bool SaveCurrentInboundFromUi()
    {
        var doc = CurrentDocument();
        if (doc?.CurrentInbound is null)
            return false;

        if (_isSaving)
        {
            _saveAgainAfterCurrent = true;
            return false;
        }

        var selectedId = doc.CurrentInbound.Id;
        _isSaving = true;
        try
        {
            _autoSaveTimer.Stop();
            CommitCurrentEditors();
            var detailTabs = CurrentDetailTabs();
            if (detailTabs is not null)
            {
                if (detailTabs.Tag is BindingSource source)
                    source.EndEdit();
                ApplyJsonEditors(doc.CurrentInbound, detailTabs);
                ApplyClientGrid(doc, doc.CurrentInbound);
            }

            doc.Repository.SaveInbound(doc.CurrentInbound);
            RefreshVisibleGrids();
            return true;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            RevertCurrentDocument(doc, selectedId);
            return false;
        }
        finally
        {
            _isSaving = false;
            if (_saveAgainAfterCurrent)
            {
                _saveAgainAfterCurrent = false;
                BeginInvoke(new Action(() => ScheduleAutoSave(_pendingAutoSaveMessage)));
            }
        }
    }

    private void RevertCurrentDocument(DatabaseDocument doc, int selectedId)
    {
        try
        {
            LoadDocument(doc, selectedId);
            RefreshCurrentTab(doc);
            SetStatus("Save failed; reverted to database values");
        }
        catch (Exception reloadEx)
        {
            AppLog.Error(reloadEx, "Failed to reload after save error");
        }
    }

    private void CommitCurrentEditors()
    {
        ValidateChildren(ValidationConstraints.Enabled);
        foreach (var grid in Descendants<DataGridView>(this))
        {
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            grid.EndEdit(DataGridViewDataErrorContexts.Commit);
        }
        foreach (var binding in Descendants<Control>(this).SelectMany(c => c.DataBindings.Cast<Binding>()))
            binding.WriteValue();
    }

    private void CommitBindingsOnly()
    {
        foreach (var binding in Descendants<Control>(this).SelectMany(c => c.DataBindings.Cast<Binding>()))
            binding.WriteValue();
    }

    private void RefreshVisibleGrids()
    {
        foreach (var grid in Descendants<DataGridView>(this))
            grid.Refresh();
    }

    private static void ApplyJsonEditors(InboundRow inbound, TabControl detailTabs)
    {
        var jsonPage = detailTabs.TabPages.Cast<TabPage>().FirstOrDefault(p => p.Text == "JSON");
        if (jsonPage is null || jsonPage.Controls.Count == 0 || jsonPage.Controls[0] is not TableLayoutPanel panel)
            return;

        var boxes = panel.Controls.OfType<TextBox>().ToList();
        var settings = boxes.FirstOrDefault(x => Equals(x.Tag, "settings"))?.Text ?? inbound.Settings;
        var stream = boxes.FirstOrDefault(x => Equals(x.Tag, "stream"))?.Text ?? inbound.StreamSettings;
        var sniffing = boxes.FirstOrDefault(x => Equals(x.Tag, "sniffing"))?.Text ?? inbound.Sniffing;

        inbound.Settings = string.IsNullOrWhiteSpace(settings) ? "{\"clients\":[]}" : JsonUtil.NormalizeObjectJson(settings);
        inbound.StreamSettings = string.IsNullOrWhiteSpace(stream) ? "" : JsonUtil.NormalizeObjectJson(stream);
        inbound.Sniffing = string.IsNullOrWhiteSpace(sniffing) ? "" : JsonUtil.NormalizeObjectJson(sniffing);
    }

    private static void ApplyClientGrid(DatabaseDocument doc, InboundRow inbound)
    {
        var settings = JsonUtil.ParseObjectOrEmpty(inbound.Settings);
        var clients = new JsonArray();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var client in doc.CurrentClients.Where(c => !string.IsNullOrWhiteSpace(c.Email)))
        {
            if (client.CreatedAt == 0)
                client.CreatedAt = now;
            client.UpdatedAt = now;
            clients.Add(JsonUtil.NodeFromClient(client));
        }

        settings["clients"] = clients;
        inbound.Settings = settings.ToJsonString(JsonUtil.PrettyJson);
    }

    private void ReloadCurrent()
    {
        var doc = CurrentDocument();
        if (doc is null)
            return;
        LoadDocument(doc);
        RefreshCurrentTab(doc);
        SetStatus("Reloaded");
    }

    private void ExportCurrentInbound()
    {
        var doc = CurrentDocument();
        if (doc?.CurrentInbound is null)
            return;
        if (!SaveCurrentInboundFromUi())
            return;

        using var dialog = new SaveFileDialog
        {
            Title = "Export inbound JSON",
            FileName = $"inbound-{doc.CurrentInbound.Id}-{SafeFile(doc.CurrentInbound.Remark)}.json",
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, doc.Repository.ExportInboundJson(doc.CurrentInbound.Id));
            SetStatus($"Exported {dialog.FileName}");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ImportInboundIntoCurrentDb()
    {
        var doc = CurrentDocument();
        if (doc is null)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Import inbound JSON exported from 3x-ui",
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var id = doc.Repository.ImportInboundJson(File.ReadAllText(dialog.FileName));
            LoadDocument(doc, id);
            RefreshCurrentTab(doc);
            SetStatus($"Imported inbound {id}");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void LoadDocument(DatabaseDocument doc, int? selectedInboundId = null)
    {
        doc.Inbounds.Clear();
        foreach (var inbound in doc.Repository.LoadInbounds())
            doc.Inbounds.Add(inbound);
        doc.CurrentInbound = selectedInboundId.HasValue
            ? doc.Inbounds.FirstOrDefault(x => x.Id == selectedInboundId.Value) ?? doc.Inbounds.FirstOrDefault()
            : doc.CurrentInbound is null
                ? doc.Inbounds.FirstOrDefault()
                : doc.Inbounds.FirstOrDefault(x => x.Id == doc.CurrentInbound.Id) ?? doc.Inbounds.FirstOrDefault();
    }

    private void RefreshCurrentTab(DatabaseDocument doc)
    {
        var page = _tabs.SelectedTab;
        if (page is null)
            return;
        page.Controls.Clear();
        page.Controls.Add(CreateDatabaseView(doc));
    }

    private DatabaseDocument? CurrentDocument()
    {
        return _tabs.SelectedTab is not null && _documents.TryGetValue(_tabs.SelectedTab, out var doc) ? doc : null;
    }

    private TabControl? CurrentDetailTabs()
    {
        if (_tabs.SelectedTab is { Controls.Count: > 0 } page && page.Controls[0] is SplitContainer split && split.Panel2.Controls.Count > 0)
            return split.Panel2.Controls[0] as TabControl;
        return null;
    }

    private static DataGridViewTextBoxColumn TextColumn(string property, string header, int width, bool readOnly = false)
    {
        return new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            HeaderText = header,
            Width = width,
            ReadOnly = readOnly
        };
    }

    private static DataGridViewCheckBoxColumn CheckColumn(string property, string header, int width)
    {
        return new DataGridViewCheckBoxColumn
        {
            DataPropertyName = property,
            HeaderText = header,
            Width = width
        };
    }

    private static string SafeFile(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(text.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "inbound" : safe;
    }

    private void SetStatus(string text) => _status.Text = text;

    private void ShowError(Exception ex)
    {
        AppLog.Error(ex, "Handled UI error");
        SetStatus("Error");
        MessageBox.Show(
            this,
            $"{ex.Message}{Environment.NewLine}{Environment.NewLine}Details were written to:{Environment.NewLine}{AppLog.LogFile}",
            "XUI DB Manager",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T typed)
                yield return typed;
            foreach (var nested in Descendants<T>(child))
                yield return nested;
        }
    }
}
