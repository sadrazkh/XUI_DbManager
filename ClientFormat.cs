using System.Globalization;

namespace XuiDbManager;

public static class ClientFormat
{
    private const long KiB = 1024;
    private const long MiB = KiB * 1024;
    private const long GiB = MiB * 1024;
    private const long TiB = GiB * 1024;

    public static string FormatTrafficBytes(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        var abs = Math.Abs((double)bytes);
        return abs switch
        {
            >= TiB => $"{bytes / (double)TiB:0.##} TB",
            >= GiB => $"{bytes / (double)GiB:0.##} GB",
            >= MiB => $"{bytes / (double)MiB:0.##} MB",
            >= KiB => $"{bytes / (double)KiB:0.##} KB",
            _ => $"{bytes} B"
        };
    }

    public static string FormatLimitBytes(long bytes) => bytes <= 0 ? "Unlimited" : FormatTrafficBytes(bytes);

    public static long ParseLimitBytes(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var value = text.Trim();
        if (value.Equals("unlimited", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("never", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            value == "-")
            return 0;

        value = value.Replace(",", "", StringComparison.Ordinal).Trim();
        var unit = "";
        var number = value;
        var split = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length >= 2)
        {
            number = split[0];
            unit = split[1];
        }
        else
        {
            var suffixStart = value.TakeWhile(ch => char.IsDigit(ch) || ch == '.' || ch == '-').Count();
            if (suffixStart < value.Length)
            {
                number = value[..suffixStart];
                unit = value[suffixStart..];
            }
        }

        if (!decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) &&
            !decimal.TryParse(number, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            throw new FormatException("Traffic limit must be a number, for example 10 GB, 500 MB, or 0 for unlimited.");

        if (parsed <= 0)
            return 0;

        var multiplier = UnitMultiplier(unit, parsed, number.Contains('.', StringComparison.Ordinal));
        return checked((long)Math.Round(parsed * multiplier, MidpointRounding.AwayFromZero));
    }

    public static string FormatExpiry(long unixMillis)
    {
        if (unixMillis <= 0)
            return "Never";

        return DateTimeOffset.FromUnixTimeMilliseconds(ToUnixMilliseconds(unixMillis)).LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    public static long ParseExpiry(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var value = text.Trim();
        if (value.Equals("never", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("unlimited", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            value == "0" ||
            value == "-")
            return 0;

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var raw))
            return ToUnixMilliseconds(raw);

        if (DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var dto) ||
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dto))
            return dto.ToUnixTimeMilliseconds();

        throw new FormatException("Expiry must be Never, a Unix timestamp, or a date like 2026-12-31 23:59.");
    }

    public static string FormatOnline(long unixValue)
    {
        if (unixValue <= 0)
            return "Never";

        return DateTimeOffset.FromUnixTimeMilliseconds(ToUnixMilliseconds(unixValue)).LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static long ToUnixMilliseconds(long value)
    {
        // 3x-ui stores expiry in milliseconds, while some traffic fields may be seconds.
        return value > 10_000_000_000 ? value : checked(value * 1000);
    }

    private static decimal UnitMultiplier(string unit, decimal parsed, bool hasDecimalPoint)
    {
        var normalized = unit.Trim().ToLowerInvariant().Replace("ib", "b", StringComparison.Ordinal);
        return normalized switch
        {
            "b" or "byte" or "bytes" => 1,
            "k" or "kb" => KiB,
            "m" or "mb" => MiB,
            "g" or "gb" => GiB,
            "t" or "tb" => TiB,
            "" => parsed >= MiB && !hasDecimalPoint ? 1 : GiB,
            _ => throw new FormatException("Traffic unit must be B, KB, MB, GB, or TB.")
        };
    }
}
