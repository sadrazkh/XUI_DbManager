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

public sealed class AllClientRow
{
    public AllClientRow(InboundRow inbound, ClientView client, ClientTrafficRow? traffic)
    {
        Inbound = inbound;
        Client = client;
        Traffic = traffic;
    }

    [Browsable(false)]
    public InboundRow Inbound { get; }

    [Browsable(false)]
    public ClientView Client { get; }

    [Browsable(false)]
    public ClientTrafficRow? Traffic { get; }

    public int InboundId => Inbound.Id;
    public string InboundRemark => Inbound.Remark;
    public string Protocol => Inbound.Protocol;
    public int Port => Inbound.Port;
    public bool Enable { get => Client.Enable; set => Client.Enable = value; }
    public string Email { get => Client.Email; set => Client.Email = value.Trim(); }
    public string Id { get => Client.Id; set => Client.Id = value.Trim(); }
    public string Password { get => Client.Password; set => Client.Password = value.Trim(); }
    public string Auth { get => Client.Auth; set => Client.Auth = value.Trim(); }
    public string Flow { get => Client.Flow; set => Client.Flow = value.Trim(); }
    public string Security { get => Client.Security; set => Client.Security = value.Trim(); }
    public string SubId { get => Client.SubId; set => Client.SubId = value.Trim(); }
    public int LimitIp { get => Client.LimitIp; set => Client.LimitIp = value; }
    public string Comment { get => Client.Comment; set => Client.Comment = value; }
    public string Group { get => Client.Group; set => Client.Group = value; }
    public string PublicKey { get => Client.PublicKey; set => Client.PublicKey = value.Trim(); }
    public string AllowedIPs { get => Client.AllowedIPs; set => Client.AllowedIPs = value.Trim(); }
    public long UpBytes => Traffic?.Up ?? 0;
    public long DownBytes => Traffic?.Down ?? 0;
    public string Up => ClientFormat.FormatTrafficBytes(UpBytes);
    public string Down => ClientFormat.FormatTrafficBytes(DownBytes);
    public string Used => ClientFormat.FormatTrafficBytes(UpBytes + DownBytes);
    public string Limit { get => ClientFormat.FormatLimitBytes(Client.TotalGB); set => Client.TotalGB = ClientFormat.ParseLimitBytes(value); }
    public string Expiry { get => ClientFormat.FormatExpiry(Client.ExpiryTime); set => Client.ExpiryTime = ClientFormat.ParseExpiry(value); }
    public string LastOnline => ClientFormat.FormatOnline(Traffic?.LastOnline ?? 0);
    public string Reset => Client.Reset == 0 ? "No" : Client.Reset.ToString();

    [Browsable(false)]
    public string SearchText => string.Join(" ", new object?[]
    {
        InboundId,
        InboundRemark,
        Protocol,
        Port,
        Enable,
        Email,
        Id,
        Password,
        Auth,
        Flow,
        Security,
        SubId,
        LimitIp,
        Comment,
        Group,
        PublicKey,
        AllowedIPs,
        Used,
        Up,
        Down,
        Limit,
        Expiry,
        LastOnline,
        Reset,
        Client.TotalGB,
        Client.ExpiryTime,
        UpBytes,
        DownBytes
    });
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
    public List<AllClientRow> AllClientRows { get; } = [];
    public BindingList<AllClientRow> FilteredAllClients { get; } = [];
    public string AllClientSearch { get; set; } = "";
}
