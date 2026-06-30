using System.Net;

namespace LanCopy.Services;

// Codigo corto de emparejamiento: empaqueta una IPv4/IPv6 + puerto en bytes y los codifica en
// Base32 (Crockford, sin caracteres ambiguos) -> ~10 caracteres (IPv4) o ~25 caracteres (IPv6).
// FEAT-004: Extended to support IPv6 addresses while maintaining IPv4 backward compatibility.
public static class PairingCode
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const byte IPv6_Version = 6; // Only used as a marker for IPv6

    public static string Encode(string ip, int port)
    {
        if (!IPAddress.TryParse(ip, out var addr))
            throw new ArgumentException("IP invalida", nameof(ip));
        if (port is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        byte[] data;
        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // IPv6: 1 byte version marker (6) + 16 bytes IPv6 + 2 bytes port = 19 bytes
            data = new byte[19];
            data[0] = IPv6_Version;
            Array.Copy(addr.GetAddressBytes(), 0, data, 1, 16);
            data[17] = (byte)(port >> 8);
            data[18] = (byte)(port & 0xFF);
        }
        else
        {
            // IPv4: 4 bytes IPv4 + 2 bytes port = 6 bytes (unchanged for backward compatibility)
            var ipBytes = addr.MapToIPv4().GetAddressBytes();
            data = new byte[6];
            Array.Copy(ipBytes, data, 4);
            data[4] = (byte)(port >> 8);
            data[5] = (byte)(port & 0xFF);
        }

        var code = ToBase32(data);
        return code.Length > 4 ? code[..4] + "-" + code[4..] : code;
    }

    public static bool TryDecode(string code, out string ip, out int port)
    {
        ip = ""; port = 0;
        if (string.IsNullOrWhiteSpace(code)) return false;
        var clean = Normalize(code);
        
        try
        {
            var data = FromBase32(clean);
            
            // Check if this is IPv6 (data[0] == 6 and length is 19)
            if (data.Length >= 19 && data[0] == IPv6_Version)
            {
                ip = new IPAddress(data[1..17]).ToString();
                port = (data[17] << 8) | data[18];
            }
            // Otherwise treat as IPv4 (original format)
            else if (data.Length >= 6)
            {
                ip = new IPAddress(new[] { data[0], data[1], data[2], data[3] }).ToString();
                port = (data[4] << 8) | data[5];
            }
            else
            {
                return false;
            }
            
            return port is >= 0 and <= 65535;
        }
        catch { return false; }
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