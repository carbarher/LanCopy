using System.Net;

namespace LanCopy.Services;

// Codigo corto de emparejamiento: empaqueta una IPv4 + puerto en 6 bytes y los codifica en
// Base32 (Crockford, sin caracteres ambiguos) -> ~10 caracteres faciles de dictar por voz.
public static class PairingCode
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Encode(string ip, int port)
    {
        if (!IPAddress.TryParse(ip, out var addr))
            throw new ArgumentException("IP invalida", nameof(ip));
        var ipBytes = addr.MapToIPv4().GetAddressBytes();
        if (port is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        var data = new byte[6];
        Array.Copy(ipBytes, data, 4);
        data[4] = (byte)(port >> 8);
        data[5] = (byte)(port & 0xFF);

        var code = ToBase32(data);
        return code.Length > 4 ? code[..4] + "-" + code[4..] : code;
    }

    public static bool TryDecode(string code, out string ip, out int port)
    {
        ip = ""; port = 0;
        if (string.IsNullOrWhiteSpace(code)) return false;
        var clean = Normalize(code);
        if (clean.Length != 10) return false;
        try
        {
            var data = FromBase32(clean);
            if (data.Length < 6) return false;
            ip = new IPAddress(new[] { data[0], data[1], data[2], data[3] }).ToString();
            port = (data[4] << 8) | data[5];
            return port is >= 0 and <= 65535;
        }
        catch (Exception ex)
        {
            Log.Debug("pairing", "decode-failed", new { error = ex.Message });
            return false;
        }
    }

    private static string Normalize(string code)
    {
        var s = code.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
        s = s.Replace('I', '1').Replace('L', '1').Replace('O', '0').Replace('U', 'V');
        return s;
    }

    private static string ToBase32(byte[] data)
    {
        var bits = 0; var value = 0;
        var sb = new System.Text.StringBuilder();
        foreach (var b in data)
        {
            value = (value << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(value >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
            sb.Append(Alphabet[(value << (5 - bits)) & 31]);
        return sb.ToString();
    }

    private static byte[] FromBase32(string s)
    {
        var bits = 0; var value = 0;
        var output = new List<byte>();
        foreach (var c in s)
        {
            var idx = Alphabet.IndexOf(c);
            if (idx < 0) throw new FormatException($"Caracter invalido: {c}");
            value = (value << 5) | idx;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}