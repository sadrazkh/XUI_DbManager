using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace XuiDbManager;

public static class SelfTest
{
    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "xui-dbmanager-selftest");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "x-ui.db");
        if (File.Exists(dbPath))
            File.Delete(dbPath);

        CreateSampleDb(dbPath);
        using var repo = new XuiRepository(dbPath);
        var inbounds = repo.LoadInbounds();
        if (inbounds.Count != 1)
            throw new InvalidOperationException("Self-test failed: inbound count mismatch.");

        var inbound = inbounds[0];
        inbound.Remark = "edited";
        repo.SaveInbound(inbound);

        var exported = repo.ExportInboundJson(inbound.Id);
        var newId = repo.ImportInboundJson(exported);
        if (newId <= inbound.Id)
            throw new InvalidOperationException("Self-test failed: import did not create a new inbound.");

        TestFormatting();
        TestClientMove(repo, inbound.Id, newId);

        return 0;
    }

    private static void TestFormatting()
    {
        if (ClientFormat.ParseLimitBytes("1 GB") != 1_073_741_824)
            throw new InvalidOperationException("Self-test failed: GB traffic parsing mismatch.");
        if (ClientFormat.ParseLimitBytes("Unlimited") != 0)
            throw new InvalidOperationException("Self-test failed: unlimited traffic parsing mismatch.");
        if (ClientFormat.ParseExpiry("Never") != 0)
            throw new InvalidOperationException("Self-test failed: expiry parsing mismatch.");
        if (string.IsNullOrWhiteSpace(ClientFormat.FormatTrafficBytes(1_073_741_824)))
            throw new InvalidOperationException("Self-test failed: traffic formatting returned empty text.");
    }

    private static void TestClientMove(XuiRepository repo, int sourceId, int destinationId)
    {
        var inbounds = repo.LoadInbounds();
        var source = inbounds.First(x => x.Id == sourceId);
        var destination = inbounds.First(x => x.Id == destinationId);

        var sourceSettings = JsonUtil.ParseObjectOrEmpty(source.Settings);
        var sourceClients = JsonUtil.GetClientsArray(sourceSettings);
        if (sourceClients.Count == 0)
            throw new InvalidOperationException("Self-test failed: source client missing before move.");

        var moved = sourceClients[0]!.DeepClone();
        sourceClients.RemoveAt(0);
        source.Settings = sourceSettings.ToJsonString(JsonUtil.PrettyJson);

        var destinationSettings = JsonUtil.ParseObjectOrEmpty(destination.Settings);
        destinationSettings["clients"] = new JsonArray(moved);
        destination.Settings = destinationSettings.ToJsonString(JsonUtil.PrettyJson);

        repo.SaveInbounds([destination, source]);

        var afterMove = repo.LoadInbounds();
        var movedSource = afterMove.First(x => x.Id == sourceId);
        var movedDestination = afterMove.First(x => x.Id == destinationId);
        if (JsonUtil.GetClientsArray(JsonUtil.ParseObjectOrEmpty(movedSource.Settings)).Count != 0)
            throw new InvalidOperationException("Self-test failed: moved client still exists in source inbound.");
        if (JsonUtil.GetClientsArray(JsonUtil.ParseObjectOrEmpty(movedDestination.Settings)).Count != 1)
            throw new InvalidOperationException("Self-test failed: moved client was not saved in destination inbound.");
    }

    private static void CreateSampleDb(string path)
    {
        using var c = new SqliteConnection($"Data Source={path}");
        c.Open();
        var sql = """
CREATE TABLE users (id integer primary key autoincrement, username text, password text, login_epoch integer default 0);
INSERT INTO users (username, password) VALUES ('admin', 'admin');
CREATE TABLE inbounds (
  id integer primary key autoincrement,
  user_id integer, up integer, down integer, total integer, remark text,
  sub_sort_index integer default 1, enable integer, expiry_time integer,
  traffic_reset text default 'never', last_traffic_reset_time integer default 0,
  listen text, port integer, protocol text, settings text, stream_settings text,
  tag text unique, sniffing text, node_id integer, share_addr_strategy text default 'node',
  share_addr text, origin_node_guid text
);
CREATE TABLE client_traffics (
  id integer primary key autoincrement, inbound_id integer, enable integer,
  email text unique, up integer, down integer, expiry_time integer, total integer,
  reset integer default 0, last_online integer default 0
);
CREATE TABLE clients (
  id integer primary key autoincrement, email text unique, sub_id text, uuid text,
  password text, auth text, flow text, security text, reverse text,
  wg_private_key text, wg_public_key text, wg_allowed_ips text,
  wg_pre_shared_key text, wg_keep_alive integer default 0,
  secret text, ad_tag text default '', limit_ip integer, total_gb integer,
  expiry_time integer, enable integer default 1, tg_id integer,
  group_name text default '', comment text, reset integer default 0,
  created_at integer, updated_at integer
);
CREATE TABLE client_inbounds (
  client_id integer, inbound_id integer, flow_override text, created_at integer,
  primary key (client_id, inbound_id)
);
""";
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();

        using var insert = c.CreateCommand();
        insert.CommandText = """
INSERT INTO inbounds (user_id, up, down, total, remark, enable, listen, port, protocol, settings, stream_settings, tag, sniffing)
VALUES (1, 0, 0, 0, 'sample-vless', 1, '', 443, 'vless', @settings, @stream, 'in-443-tcp', @sniffing)
""";
        insert.Parameters.AddWithValue("@settings", """{"clients":[{"id":"11111111-2222-4333-8444-555555555555","email":"u1","enable":true,"subId":"sub-1","totalGB":1073741824,"expiryTime":0}],"decryption":"none"}""");
        insert.Parameters.AddWithValue("@stream", """{"network":"tcp","security":"reality","tcpSettings":{},"realitySettings":{"serverNames":["example.com"],"shortIds":["abcd"],"settings":{"publicKey":"pub","fingerprint":"chrome"}}}""");
        insert.Parameters.AddWithValue("@sniffing", """{"enabled":true,"destOverride":["http","tls","quic"]}""");
        insert.ExecuteNonQuery();
    }
}
