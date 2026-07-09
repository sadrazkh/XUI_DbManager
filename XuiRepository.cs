using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace XuiDbManager;

public sealed class XuiRepository : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Dictionary<string, HashSet<string>> _columns = new(StringComparer.OrdinalIgnoreCase);

    public XuiRepository(string path)
    {
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        _connection.Open();
        LoadTableColumns();
        EnsureLooksLikeXuiDb();
    }

    public void Dispose() => _connection.Dispose();

    public List<InboundRow> LoadInbounds()
    {
        var result = new List<InboundRow>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM inbounds ORDER BY id ASC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadInbound(reader));

        foreach (var inbound in result)
            inbound.ClientStats = LoadClientStats(inbound.Id);

        return result;
    }

    public string ExportInboundJson(int inboundId)
    {
        var inbound = LoadInbounds().First(x => x.Id == inboundId);
        var obj = new JsonObject
        {
            ["id"] = inbound.Id,
            ["up"] = inbound.Up,
            ["down"] = inbound.Down,
            ["total"] = inbound.Total,
            ["remark"] = inbound.Remark,
            ["subSortIndex"] = inbound.SubSortIndex,
            ["enable"] = inbound.Enable,
            ["expiryTime"] = inbound.ExpiryTime,
            ["trafficReset"] = inbound.TrafficReset,
            ["lastTrafficResetTime"] = inbound.LastTrafficResetTime,
            ["listen"] = inbound.Listen,
            ["port"] = inbound.Port,
            ["protocol"] = inbound.Protocol,
            ["settings"] = JsonUtil.ParseObjectOrEmpty(inbound.Settings),
            ["streamSettings"] = string.IsNullOrWhiteSpace(inbound.StreamSettings) ? null : JsonUtil.ParseObjectOrEmpty(inbound.StreamSettings),
            ["tag"] = inbound.Tag,
            ["sniffing"] = string.IsNullOrWhiteSpace(inbound.Sniffing) ? null : JsonUtil.ParseObjectOrEmpty(inbound.Sniffing),
            ["shareAddrStrategy"] = inbound.ShareAddrStrategy,
            ["shareAddr"] = inbound.ShareAddr,
            ["clientStats"] = new JsonArray(inbound.ClientStats.Select(ExportTraffic).ToArray())
        };

        if (inbound.NodeId.HasValue)
            obj["nodeId"] = inbound.NodeId.Value;
        if (!string.IsNullOrWhiteSpace(inbound.OriginNodeGuid))
            obj["originNodeGuid"] = inbound.OriginNodeGuid;

        return obj.ToJsonString(JsonUtil.PrettyJson);
    }

    public int ImportInboundJson(string json)
    {
        var node = JsonNode.Parse(json) as JsonObject ?? throw new InvalidOperationException("Inbound export must be a JSON object.");
        var inbound = InboundFromJson(node);
        inbound.Id = 0;
        inbound.NodeId = null;
        inbound.UserId = DefaultUserId();
        inbound.Tag = UniqueTag(inbound.Tag, inbound.Port, inbound.Protocol);

        using var tx = _connection.BeginTransaction();
        var newId = InsertInbound(inbound, tx);
        inbound.Id = newId;
        SaveClientStats(inbound, tx);
        SyncNormalizedClients(inbound, tx);
        tx.Commit();
        return newId;
    }

    public void SaveInbound(InboundRow inbound)
    {
        JsonUtil.NormalizeObjectJson(inbound.Settings);
        if (!string.IsNullOrWhiteSpace(inbound.StreamSettings))
            JsonUtil.NormalizeObjectJson(inbound.StreamSettings);
        if (!string.IsNullOrWhiteSpace(inbound.Sniffing))
            JsonUtil.NormalizeObjectJson(inbound.Sniffing);

        using var tx = _connection.BeginTransaction();
        var updates = new Dictionary<string, object?>
        {
            ["user_id"] = inbound.UserId,
            ["up"] = inbound.Up,
            ["down"] = inbound.Down,
            ["total"] = inbound.Total,
            ["remark"] = inbound.Remark,
            ["sub_sort_index"] = inbound.SubSortIndex,
            ["enable"] = inbound.Enable ? 1 : 0,
            ["expiry_time"] = inbound.ExpiryTime,
            ["traffic_reset"] = inbound.TrafficReset,
            ["last_traffic_reset_time"] = inbound.LastTrafficResetTime,
            ["listen"] = inbound.Listen,
            ["port"] = inbound.Port,
            ["protocol"] = inbound.Protocol,
            ["settings"] = inbound.Settings,
            ["stream_settings"] = inbound.StreamSettings,
            ["tag"] = inbound.Tag,
            ["sniffing"] = inbound.Sniffing,
            ["node_id"] = inbound.NodeId,
            ["share_addr_strategy"] = string.IsNullOrWhiteSpace(inbound.ShareAddrStrategy) ? "node" : inbound.ShareAddrStrategy,
            ["share_addr"] = inbound.ShareAddr,
            ["origin_node_guid"] = inbound.OriginNodeGuid
        };

        ExecuteUpdate("inbounds", updates, "id = @id", new Dictionary<string, object?> { ["id"] = inbound.Id }, tx);
        SaveClientStats(inbound, tx);
        SyncNormalizedClients(inbound, tx);
        tx.Commit();
    }

    private int InsertInbound(InboundRow inbound, SqliteTransaction tx)
    {
        var values = new Dictionary<string, object?>
        {
            ["user_id"] = inbound.UserId,
            ["up"] = inbound.Up,
            ["down"] = inbound.Down,
            ["total"] = inbound.Total,
            ["remark"] = inbound.Remark,
            ["sub_sort_index"] = inbound.SubSortIndex,
            ["enable"] = inbound.Enable ? 1 : 0,
            ["expiry_time"] = inbound.ExpiryTime,
            ["traffic_reset"] = string.IsNullOrWhiteSpace(inbound.TrafficReset) ? "never" : inbound.TrafficReset,
            ["last_traffic_reset_time"] = inbound.LastTrafficResetTime,
            ["listen"] = inbound.Listen,
            ["port"] = inbound.Port,
            ["protocol"] = inbound.Protocol,
            ["settings"] = inbound.Settings,
            ["stream_settings"] = inbound.StreamSettings,
            ["tag"] = inbound.Tag,
            ["sniffing"] = inbound.Sniffing,
            ["node_id"] = inbound.NodeId,
            ["share_addr_strategy"] = string.IsNullOrWhiteSpace(inbound.ShareAddrStrategy) ? "node" : inbound.ShareAddrStrategy,
            ["share_addr"] = inbound.ShareAddr,
            ["origin_node_guid"] = inbound.OriginNodeGuid
        };

        values = ExistingValues("inbounds", values);
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"INSERT INTO inbounds ({string.Join(", ", values.Keys)}) VALUES ({string.Join(", ", values.Keys.Select(k => "@" + k))}); SELECT last_insert_rowid();";
        AddParameters(cmd, values);
        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    private void SaveClientStats(InboundRow inbound, SqliteTransaction tx)
    {
        if (!HasTable("client_traffics"))
            return;

        var clients = ParseClients(inbound.Settings);
        var emails = clients.Select(c => c.Email).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var importedStats = inbound.ClientStats
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var client in clients)
        {
            if (string.IsNullOrWhiteSpace(client.Email))
                continue;

            importedStats.TryGetValue(client.Email, out var stat);
            var values = new Dictionary<string, object?>
            {
                ["inbound_id"] = inbound.Id,
                ["enable"] = client.Enable ? 1 : 0,
                ["email"] = client.Email,
                ["up"] = stat?.Up ?? ExistingTrafficLong(client.Email, "up", tx),
                ["down"] = stat?.Down ?? ExistingTrafficLong(client.Email, "down", tx),
                ["expiry_time"] = client.ExpiryTime,
                ["total"] = client.TotalGB,
                ["reset"] = client.Reset,
                ["last_online"] = stat?.LastOnline ?? ExistingTrafficLong(client.Email, "last_online", tx)
            };

            if (TrafficExists(client.Email, tx))
                ExecuteUpdate("client_traffics", values, "email = @email", new Dictionary<string, object?> { ["email"] = client.Email }, tx);
            else
                ExecuteInsert("client_traffics", values, tx);
        }

        foreach (var old in LoadClientStats(inbound.Id, tx))
        {
            if (emails.Contains(old.Email, StringComparer.OrdinalIgnoreCase))
                continue;
            if (EmailUsedByOtherInbound(old.Email, inbound.Id, tx))
                continue;
            ExecuteNonQuery("DELETE FROM client_traffics WHERE email = @email", new Dictionary<string, object?> { ["email"] = old.Email }, tx);
        }
    }

    private void SyncNormalizedClients(InboundRow inbound, SqliteTransaction tx)
    {
        if (!HasTable("clients") || !HasTable("client_inbounds"))
            return;

        var clients = ParseClients(inbound.Settings)
            .Where(c => !string.IsNullOrWhiteSpace(c.Email))
            .GroupBy(c => c.Email, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        ExecuteNonQuery("DELETE FROM client_inbounds WHERE inbound_id = @id", new Dictionary<string, object?> { ["id"] = inbound.Id }, tx);

        foreach (var client in clients)
        {
            var clientId = UpsertClientRecord(client, tx);
            var link = new Dictionary<string, object?>
            {
                ["client_id"] = clientId,
                ["inbound_id"] = inbound.Id,
                ["flow_override"] = client.Flow,
                ["created_at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            ExecuteInsert("client_inbounds", link, tx);
        }
    }

    private int UpsertClientRecord(ClientView client, SqliteTransaction tx)
    {
        var existingId = ScalarInt("SELECT id FROM clients WHERE email = @email LIMIT 1", new Dictionary<string, object?> { ["email"] = client.Email }, tx);
        var values = ClientRecordValues(client);
        if (existingId.HasValue)
        {
            ExecuteUpdate("clients", values, "id = @id", new Dictionary<string, object?> { ["id"] = existingId.Value }, tx);
            return existingId.Value;
        }

        ExecuteInsert("clients", values, tx);
        return ScalarInt("SELECT id FROM clients WHERE email = @email LIMIT 1", new Dictionary<string, object?> { ["email"] = client.Email }, tx)!.Value;
    }

    private Dictionary<string, object?> ClientRecordValues(ClientView client)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new Dictionary<string, object?>
        {
            ["email"] = client.Email,
            ["sub_id"] = client.SubId,
            ["uuid"] = client.Id,
            ["password"] = client.Password,
            ["auth"] = client.Auth,
            ["flow"] = client.Flow,
            ["security"] = client.Security,
            ["wg_private_key"] = client.PrivateKey,
            ["wg_public_key"] = client.PublicKey,
            ["wg_allowed_ips"] = client.AllowedIPs,
            ["wg_pre_shared_key"] = client.PreSharedKey,
            ["wg_keep_alive"] = client.KeepAlive,
            ["secret"] = client.Secret,
            ["ad_tag"] = client.AdTag,
            ["limit_ip"] = client.LimitIp,
            ["total_gb"] = client.TotalGB,
            ["expiry_time"] = client.ExpiryTime,
            ["enable"] = client.Enable ? 1 : 0,
            ["tg_id"] = client.TgId,
            ["group_name"] = client.Group,
            ["comment"] = client.Comment,
            ["reset"] = client.Reset,
            ["created_at"] = client.CreatedAt == 0 ? now : client.CreatedAt,
            ["updated_at"] = now
        };
    }

    private List<ClientView> ParseClients(string settingsJson)
    {
        var settings = JsonUtil.ParseObjectOrEmpty(settingsJson);
        var arr = JsonUtil.GetClientsArray(settings);
        return arr.OfType<JsonObject>().Select(JsonUtil.ClientFromNode).ToList();
    }

    private static JsonNode ExportTraffic(ClientTrafficRow row)
    {
        return new JsonObject
        {
            ["id"] = row.Id,
            ["inboundId"] = row.InboundId,
            ["enable"] = row.Enable,
            ["email"] = row.Email,
            ["up"] = row.Up,
            ["down"] = row.Down,
            ["expiryTime"] = row.ExpiryTime,
            ["total"] = row.Total,
            ["reset"] = row.Reset,
            ["lastOnline"] = row.LastOnline
        };
    }

    private InboundRow InboundFromJson(JsonObject obj)
    {
        string jsonField(string name) => obj[name] is null || obj[name]!.GetValueKind() == JsonValueKind.Null ? "" : obj[name]!.ToJsonString(JsonUtil.PrettyJson);

        var inbound = new InboundRow
        {
            UserId = DefaultUserId(),
            Up = JsonUtil.GetLong(obj, "up"),
            Down = JsonUtil.GetLong(obj, "down"),
            Total = JsonUtil.GetLong(obj, "total"),
            Remark = JsonUtil.GetString(obj, "remark") ?? "",
            SubSortIndex = Math.Max(1, JsonUtil.GetInt(obj, "subSortIndex")),
            Enable = JsonUtil.GetBool(obj, "enable", true),
            ExpiryTime = JsonUtil.GetLong(obj, "expiryTime"),
            TrafficReset = JsonUtil.GetString(obj, "trafficReset") ?? "never",
            LastTrafficResetTime = JsonUtil.GetLong(obj, "lastTrafficResetTime"),
            Listen = JsonUtil.GetString(obj, "listen") ?? "",
            Port = JsonUtil.GetInt(obj, "port"),
            Protocol = JsonUtil.GetString(obj, "protocol") ?? "vless",
            Settings = jsonField("settings"),
            StreamSettings = jsonField("streamSettings"),
            Tag = JsonUtil.GetString(obj, "tag") ?? "",
            Sniffing = jsonField("sniffing"),
            ShareAddrStrategy = JsonUtil.GetString(obj, "shareAddrStrategy") ?? "node",
            ShareAddr = JsonUtil.GetString(obj, "shareAddr") ?? "",
            OriginNodeGuid = JsonUtil.GetString(obj, "originNodeGuid") ?? ""
        };

        if (obj["clientStats"] is JsonArray stats)
        {
            inbound.ClientStats = stats.OfType<JsonObject>().Select(s => new ClientTrafficRow
            {
                InboundId = 0,
                Enable = JsonUtil.GetBool(s, "enable", true),
                Email = JsonUtil.GetString(s, "email") ?? "",
                Up = JsonUtil.GetLong(s, "up"),
                Down = JsonUtil.GetLong(s, "down"),
                ExpiryTime = JsonUtil.GetLong(s, "expiryTime"),
                Total = JsonUtil.GetLong(s, "total"),
                Reset = JsonUtil.GetInt(s, "reset"),
                LastOnline = JsonUtil.GetLong(s, "lastOnline")
            }).ToList();
        }

        if (string.IsNullOrWhiteSpace(inbound.Settings))
            inbound.Settings = "{\"clients\":[]}";

        return inbound;
    }

    private InboundRow ReadInbound(IDataRecord reader)
    {
        return new InboundRow
        {
            Id = GetInt(reader, "id"),
            UserId = GetInt(reader, "user_id"),
            Up = GetLong(reader, "up"),
            Down = GetLong(reader, "down"),
            Total = GetLong(reader, "total"),
            Remark = GetString(reader, "remark"),
            SubSortIndex = Math.Max(1, GetInt(reader, "sub_sort_index", 1)),
            Enable = GetBool(reader, "enable", true),
            ExpiryTime = GetLong(reader, "expiry_time"),
            TrafficReset = GetString(reader, "traffic_reset", "never"),
            LastTrafficResetTime = GetLong(reader, "last_traffic_reset_time"),
            Listen = GetString(reader, "listen"),
            Port = GetInt(reader, "port"),
            Protocol = GetString(reader, "protocol"),
            Settings = GetString(reader, "settings", "{\"clients\":[]}"),
            StreamSettings = GetString(reader, "stream_settings"),
            Tag = GetString(reader, "tag"),
            Sniffing = GetString(reader, "sniffing"),
            NodeId = IsNull(reader, "node_id") ? null : GetInt(reader, "node_id"),
            ShareAddrStrategy = GetString(reader, "share_addr_strategy", "node"),
            ShareAddr = GetString(reader, "share_addr"),
            OriginNodeGuid = GetString(reader, "origin_node_guid")
        };
    }

    private List<ClientTrafficRow> LoadClientStats(int inboundId, SqliteTransaction? tx = null)
    {
        if (!HasTable("client_traffics"))
            return [];

        var rows = new List<ClientTrafficRow>();
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT * FROM client_traffics WHERE inbound_id = @id ORDER BY id ASC";
        cmd.Parameters.AddWithValue("@id", inboundId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ClientTrafficRow
            {
                Id = GetInt(reader, "id"),
                InboundId = GetInt(reader, "inbound_id"),
                Enable = GetBool(reader, "enable", true),
                Email = GetString(reader, "email"),
                Up = GetLong(reader, "up"),
                Down = GetLong(reader, "down"),
                ExpiryTime = GetLong(reader, "expiry_time"),
                Total = GetLong(reader, "total"),
                Reset = GetInt(reader, "reset"),
                LastOnline = GetLong(reader, "last_online")
            });
        }
        return rows;
    }

    private bool EmailUsedByOtherInbound(string email, int inboundId, SqliteTransaction? tx = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT settings FROM inbounds WHERE id <> @id";
        cmd.Parameters.AddWithValue("@id", inboundId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var settings = reader.IsDBNull(0) ? "" : reader.GetString(0);
            if (ParseClients(settings).Any(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    private bool TrafficExists(string email, SqliteTransaction? tx = null)
    {
        return ScalarInt("SELECT id FROM client_traffics WHERE email = @email LIMIT 1", new Dictionary<string, object?> { ["email"] = email }, tx).HasValue;
    }

    private long ExistingTrafficLong(string email, string column, SqliteTransaction? tx = null)
    {
        if (!HasColumn("client_traffics", column))
            return 0;
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT {column} FROM client_traffics WHERE email = @email LIMIT 1";
        cmd.Parameters.AddWithValue("@email", email);
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt64(value);
    }

    private int DefaultUserId()
    {
        if (!HasTable("users"))
            return 1;
        return ScalarInt("SELECT id FROM users ORDER BY id ASC LIMIT 1", [], null) ?? 1;
    }

    private string UniqueTag(string tag, int port, string protocol)
    {
        if (string.IsNullOrWhiteSpace(tag))
            tag = $"in-{port}-{protocol}";

        var candidate = tag;
        var suffix = 1;
        while (ScalarInt("SELECT id FROM inbounds WHERE tag = @tag LIMIT 1", new Dictionary<string, object?> { ["tag"] = candidate }, null).HasValue)
            candidate = $"{tag}-import-{suffix++}";
        return candidate;
    }

    private void ExecuteInsert(string table, Dictionary<string, object?> values, SqliteTransaction tx)
    {
        values = ExistingValues(table, values);
        if (values.Count == 0)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"INSERT INTO {table} ({string.Join(", ", values.Keys)}) VALUES ({string.Join(", ", values.Keys.Select(k => "@" + k))})";
        AddParameters(cmd, values);
        cmd.ExecuteNonQuery();
    }

    private void ExecuteUpdate(string table, Dictionary<string, object?> values, string where, Dictionary<string, object?> whereParams, SqliteTransaction tx)
    {
        values = ExistingValues(table, values);
        if (values.Count == 0)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"UPDATE {table} SET {string.Join(", ", values.Keys.Select(k => k + " = @" + k))} WHERE {where}";
        AddParameters(cmd, values);
        AddParameters(cmd, whereParams);
        cmd.ExecuteNonQuery();
    }

    private void ExecuteNonQuery(string sql, Dictionary<string, object?> values, SqliteTransaction tx)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        AddParameters(cmd, values);
        cmd.ExecuteNonQuery();
    }

    private int? ScalarInt(string sql, Dictionary<string, object?> values, SqliteTransaction? tx)
    {
        using var cmd = _connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        AddParameters(cmd, values);
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToInt32(value);
    }

    private Dictionary<string, object?> ExistingValues(string table, Dictionary<string, object?> values)
    {
        return values.Where(kv => HasColumn(table, kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static void AddParameters(SqliteCommand cmd, Dictionary<string, object?> values)
    {
        foreach (var (key, value) in values)
        {
            var parameterName = "@" + key;
            if (cmd.Parameters.Contains(parameterName))
            {
                cmd.Parameters[parameterName].Value = value ?? DBNull.Value;
                continue;
            }
            cmd.Parameters.AddWithValue(parameterName, value ?? DBNull.Value);
        }
    }

    private void LoadTableColumns()
    {
        using var tableCmd = _connection.CreateCommand();
        tableCmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var tableReader = tableCmd.ExecuteReader();
        var tables = new List<string>();
        while (tableReader.Read())
            tables.Add(tableReader.GetString(0));

        foreach (var table in tables)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info('{table.Replace("'", "''")}')";
            using var reader = cmd.ExecuteReader();
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
                set.Add(reader.GetString(1));
            _columns[table] = set;
        }
    }

    private void EnsureLooksLikeXuiDb()
    {
        if (!HasTable("inbounds") || !HasColumn("inbounds", "settings"))
            throw new InvalidOperationException("This file does not look like a 3x-ui/x-ui SQLite database. The inbounds table was not found.");
    }

    private bool HasTable(string table) => _columns.ContainsKey(table);
    private bool HasColumn(string table, string column) => _columns.TryGetValue(table, out var cols) && cols.Contains(column);

    private static bool HasReaderColumn(IDataRecord reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static bool IsNull(IDataRecord reader, string name) => !HasReaderColumn(reader, name) || reader[name] is DBNull;
    private static string GetString(IDataRecord reader, string name, string fallback = "") => IsNull(reader, name) ? fallback : Convert.ToString(reader[name]) ?? fallback;
    private static int GetInt(IDataRecord reader, string name, int fallback = 0) => IsNull(reader, name) ? fallback : Convert.ToInt32(reader[name]);
    private static long GetLong(IDataRecord reader, string name, long fallback = 0) => IsNull(reader, name) ? fallback : Convert.ToInt64(reader[name]);
    private static bool GetBool(IDataRecord reader, string name, bool fallback = false) => IsNull(reader, name) ? fallback : Convert.ToInt32(reader[name]) != 0;
}
