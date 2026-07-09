using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XuiDbManager;

public static class JsonUtil
{
    public static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static JsonObject ParseObjectOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
            return [];

        var node = JsonNode.Parse(json);
        if (node is JsonObject obj)
            return obj;

        throw new InvalidOperationException("JSON root must be an object.");
    }

    public static string NormalizeObjectJson(string? json)
    {
        return ParseObjectOrEmpty(json).ToJsonString(PrettyJson);
    }

    public static JsonArray GetClientsArray(JsonObject settings)
    {
        if (settings["clients"] is JsonArray arr)
            return arr;

        arr = [];
        settings["clients"] = arr;
        return arr;
    }

    public static string? GetString(JsonObject obj, string name) => obj[name]?.GetValue<string>();

    public static int GetInt(JsonObject obj, string name)
    {
        var node = obj[name];
        if (node is null) return 0;
        return node.GetValueKind() switch
        {
            JsonValueKind.Number => node.GetValue<int>(),
            JsonValueKind.String when int.TryParse(node.GetValue<string>(), out var n) => n,
            _ => 0
        };
    }

    public static long GetLong(JsonObject obj, string name)
    {
        var node = obj[name];
        if (node is null) return 0;
        return node.GetValueKind() switch
        {
            JsonValueKind.Number => node.GetValue<long>(),
            JsonValueKind.String when long.TryParse(node.GetValue<string>(), out var n) => n,
            _ => 0
        };
    }

    public static bool GetBool(JsonObject obj, string name, bool fallback = false)
    {
        var node = obj[name];
        if (node is null) return fallback;
        return node.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => node.GetValue<int>() != 0,
            JsonValueKind.String when bool.TryParse(node.GetValue<string>(), out var b) => b,
            _ => fallback
        };
    }

    public static ClientView ClientFromNode(JsonObject obj)
    {
        var allowed = obj["allowedIPs"] is JsonArray arr
            ? string.Join(",", arr.Select(x => x?.GetValue<string>()).Where(x => !string.IsNullOrWhiteSpace(x)))
            : GetString(obj, "allowedIPs") ?? "";

        return new ClientView
        {
            Email = GetString(obj, "email") ?? "",
            Enable = GetBool(obj, "enable", true),
            Id = GetString(obj, "id") ?? "",
            Password = GetString(obj, "password") ?? "",
            Auth = GetString(obj, "auth") ?? "",
            Flow = GetString(obj, "flow") ?? "",
            Security = GetString(obj, "security") ?? "",
            LimitIp = GetInt(obj, "limitIp"),
            TotalGB = GetLong(obj, "totalGB"),
            ExpiryTime = GetLong(obj, "expiryTime"),
            Reset = GetInt(obj, "reset"),
            TgId = GetLong(obj, "tgId"),
            SubId = GetString(obj, "subId") ?? "",
            Group = GetString(obj, "group") ?? "",
            Comment = GetString(obj, "comment") ?? "",
            Secret = GetString(obj, "secret") ?? "",
            AdTag = GetString(obj, "adTag") ?? "",
            PrivateKey = GetString(obj, "privateKey") ?? "",
            PublicKey = GetString(obj, "publicKey") ?? "",
            AllowedIPs = allowed,
            PreSharedKey = GetString(obj, "preSharedKey") ?? "",
            KeepAlive = GetInt(obj, "keepAlive"),
            CreatedAt = GetLong(obj, "created_at"),
            UpdatedAt = GetLong(obj, "updated_at"),
            Source = obj
        };
    }

    public static JsonObject NodeFromClient(ClientView client)
    {
        var obj = client.Source.DeepClone() as JsonObject ?? [];
        Set(obj, "email", client.Email);
        Set(obj, "enable", client.Enable);
        Set(obj, "id", client.Id);
        Set(obj, "password", client.Password);
        Set(obj, "auth", client.Auth);
        Set(obj, "flow", client.Flow);
        Set(obj, "security", client.Security);
        Set(obj, "limitIp", client.LimitIp);
        Set(obj, "totalGB", client.TotalGB);
        Set(obj, "expiryTime", client.ExpiryTime);
        Set(obj, "reset", client.Reset);
        Set(obj, "tgId", client.TgId);
        Set(obj, "subId", client.SubId);
        Set(obj, "group", client.Group);
        Set(obj, "comment", client.Comment);
        Set(obj, "secret", client.Secret);
        Set(obj, "adTag", client.AdTag);
        Set(obj, "privateKey", client.PrivateKey);
        Set(obj, "publicKey", client.PublicKey);
        Set(obj, "preSharedKey", client.PreSharedKey);
        Set(obj, "keepAlive", client.KeepAlive);
        Set(obj, "created_at", client.CreatedAt);
        Set(obj, "updated_at", client.UpdatedAt);

        if (!string.IsNullOrWhiteSpace(client.AllowedIPs))
        {
            var arr = new JsonArray();
            foreach (var item in client.AllowedIPs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                arr.Add(item);
            obj["allowedIPs"] = arr;
        }
        else
        {
            obj.Remove("allowedIPs");
        }

        return obj;
    }

    private static void Set(JsonObject obj, string name, string value)
    {
        if (string.IsNullOrEmpty(value))
            obj.Remove(name);
        else
            obj[name] = value;
    }

    private static void Set(JsonObject obj, string name, bool value) => obj[name] = value;

    private static void Set(JsonObject obj, string name, int value)
    {
        if (value == 0)
            obj.Remove(name);
        else
            obj[name] = value;
    }

    private static void Set(JsonObject obj, string name, long value)
    {
        if (value == 0)
            obj.Remove(name);
        else
            obj[name] = value;
    }
}
