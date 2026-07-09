using System.ComponentModel;
using System.Text.Json.Nodes;

namespace XuiDbManager;

public sealed class InboundRow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public long Up { get; set; }
    public long Down { get; set; }
    public long Total { get; set; }
    public string Remark { get; set; } = "";
    public int SubSortIndex { get; set; } = 1;
    public bool Enable { get; set; } = true;
    public long ExpiryTime { get; set; }
    public string TrafficReset { get; set; } = "never";
    public long LastTrafficResetTime { get; set; }
    public string Listen { get; set; } = "";
    public int Port { get; set; }
    public string Protocol { get; set; } = "vless";
    public string Settings { get; set; } = "{\"clients\":[]}";
    public string StreamSettings { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Sniffing { get; set; } = "";
    public int? NodeId { get; set; }
    public string ShareAddrStrategy { get; set; } = "node";
    public string ShareAddr { get; set; } = "";
    public string OriginNodeGuid { get; set; } = "";

    [Browsable(false)]
    public List<ClientTrafficRow> ClientStats { get; set; } = [];
}

public sealed class ClientTrafficRow
{
    public int Id { get; set; }
    public int InboundId { get; set; }
    public bool Enable { get; set; } = true;
    public string Email { get; set; } = "";
    public long Up { get; set; }
    public long Down { get; set; }
    public long ExpiryTime { get; set; }
    public long Total { get; set; }
    public int Reset { get; set; }
    public long LastOnline { get; set; }
}

public sealed class ClientView
{
    public string Email { get; set; } = "";
    public bool Enable { get; set; } = true;
    public string Id { get; set; } = "";
    public string Password { get; set; } = "";
    public string Auth { get; set; } = "";
    public string Flow { get; set; } = "";
    public string Security { get; set; } = "";
    public int LimitIp { get; set; }
    public long TotalGB { get; set; }
    public long ExpiryTime { get; set; }
    public int Reset { get; set; }
    public long TgId { get; set; }
    public string SubId { get; set; } = "";
    public string Group { get; set; } = "";
    public string Comment { get; set; } = "";
    public string Secret { get; set; } = "";
    public string AdTag { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string AllowedIPs { get; set; } = "";
    public string PreSharedKey { get; set; } = "";
    public int KeepAlive { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }

    [Browsable(false)]
    public JsonObject Source { get; set; } = [];
}

public sealed class DatabaseDocument
{
    public DatabaseDocument(string path, XuiRepository repository)
    {
        Path = path;
        Repository = repository;
    }

    public string Path { get; }
    public XuiRepository Repository { get; }
    public BindingList<InboundRow> Inbounds { get; } = [];
    public InboundRow? CurrentInbound { get; set; }
    public BindingList<ClientView> CurrentClients { get; } = [];
}
