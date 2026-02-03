using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

class TestAMuleConnection
{
    static async Task Main(string[] args)
    {
        string password = "Carlos66*";
        string host = "localhost";
        int port = 4712;

        Console.WriteLine("=== Test de conexión a aMule EC ===");
        Console.WriteLine($"Host: {host}");
        Console.WriteLine($"Puerto: {port}");
        Console.WriteLine($"Contraseña: {password}");
        Console.WriteLine();

        // Calcular MD5
        using (var md5 = MD5.Create())
        {
            var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(password));
            var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            Console.WriteLine($"MD5 de contraseña: {hashHex}");
        }
        Console.WriteLine();

        try
        {
            // Conectar
            Console.WriteLine("1. Conectando al puerto TCP...");
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(host, port);
                Console.WriteLine("   ✓ Conectado");

                using (var stream = client.GetStream())
                {
                    // PASO 1: Enviar AUTH_REQ
                    Console.WriteLine();
                    Console.WriteLine("2. Enviando AUTH_REQ...");
                    
                    var authReqPacket = BuildAuthReqPacket();
                    await stream.WriteAsync(authReqPacket, 0, authReqPacket.Length);
                    await stream.FlushAsync();
                    Console.WriteLine($"   ✓ Enviados {authReqPacket.Length} bytes");
                    Console.WriteLine($"   Hex: {BitConverter.ToString(authReqPacket).Replace("-", " ")}");

                    // Leer respuesta
                    Console.WriteLine();
                    Console.WriteLine("3. Esperando respuesta AUTH_SALT...");
                    
                    var flagsBytes = new byte[4];
                    int bytesRead = await stream.ReadAsync(flagsBytes, 0, 4);
                    if (bytesRead < 4)
                    {
                        Console.WriteLine("   ✗ Conexión cerrada por aMule (no se recibieron flags)");
                        return;
                    }
                    
                    uint flags = BitConverter.ToUInt32(flagsBytes, 0);
                    Console.WriteLine($"   Flags recibidos: 0x{flags:X8}");

                    var sizeBytes = new byte[4];
                    bytesRead = await stream.ReadAsync(sizeBytes, 0, 4);
                    if (bytesRead < 4)
                    {
                        Console.WriteLine("   ✗ Conexión cerrada por aMule (no se recibió tamaño)");
                        return;
                    }
                    
                    uint bodySize = BitConverter.ToUInt32(sizeBytes, 0);
                    Console.WriteLine($"   Tamaño del cuerpo: {bodySize} bytes");

                    var body = new byte[bodySize];
                    int totalRead = 0;
                    while (totalRead < bodySize)
                    {
                        bytesRead = await stream.ReadAsync(body, totalRead, (int)(bodySize - totalRead));
                        if (bytesRead == 0)
                        {
                            Console.WriteLine("   ✗ Conexión cerrada antes de completar lectura");
                            return;
                        }
                        totalRead += bytesRead;
                    }

                    Console.WriteLine($"   ✓ Recibidos {totalRead} bytes");
                    Console.WriteLine($"   Hex: {BitConverter.ToString(body).Replace("-", " ")}");

                    // Parsear respuesta
                    byte opCode = body[0];
                    Console.WriteLine($"   OpCode: 0x{opCode:X2}");

                    if (opCode == 0x05) // EC_OP_AUTH_SALT
                    {
                        Console.WriteLine("   ✓ Recibido EC_OP_AUTH_SALT");
                        
                        // Extraer salt (simplificado)
                        // TODO: parsear correctamente los tags EC
                        Console.WriteLine();
                        Console.WriteLine("4. ÉXITO: aMule respondió correctamente");
                        Console.WriteLine("   El problema puede estar en el paso 2 (AUTH_PASSWD)");
                    }
                    else if (opCode == 0x03) // EC_OP_AUTH_FAIL
                    {
                        Console.WriteLine("   ✗ Recibido EC_OP_AUTH_FAIL");
                        Console.WriteLine("   aMule rechazó la autenticación");
                    }
                    else
                    {
                        Console.WriteLine($"   ? OpCode desconocido: 0x{opCode:X2}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine($"   Tipo: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Presiona Enter para salir...");
        Console.ReadLine();
    }

    static byte[] BuildAuthReqPacket()
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // Flags (UTF8 numbers)
            writer.Write((uint)0x00000022);

            // Body
            using (var bodyMs = new MemoryStream())
            using (var bodyWriter = new BinaryWriter(bodyMs))
            {
                // OpCode: EC_OP_AUTH_REQ (0x02)
                bodyWriter.Write((byte)0x02);

                // Tag count: 3
                bodyWriter.Write((byte)0x03);

                // Tag 1: EC_TAG_CLIENT_NAME
                WriteTag(bodyWriter, 0x0100, "SlskDown");

                // Tag 2: EC_TAG_CLIENT_VERSION
                WriteTag(bodyWriter, 0x0101, "0x0001");

                // Tag 3: EC_TAG_PROTOCOL_VERSION
                WriteTagUInt16(bodyWriter, 0x0002, 0x0204);

                var body = bodyMs.ToArray();
                writer.Write((uint)body.Length);
                writer.Write(body);
            }

            return ms.ToArray();
        }
    }

    static void WriteTag(BinaryWriter writer, ushort tagName, string value)
    {
        // Tag name (shifted left 1 bit)
        ushort nameValue = (ushort)(tagName << 1);
        writer.Write((byte)nameValue);
        writer.Write((byte)(nameValue >> 8));

        // Tag type: STRING (0x06)
        writer.Write((byte)0x06);

        // Value length
        var valueBytes = Encoding.UTF8.GetBytes(value);
        writer.Write((byte)(valueBytes.Length + 1)); // +1 for null terminator

        // Value
        writer.Write(valueBytes);
        writer.Write((byte)0); // Null terminator
    }

    static void WriteTagUInt16(BinaryWriter writer, ushort tagName, ushort value)
    {
        // Tag name (shifted left 1 bit)
        ushort nameValue = (ushort)(tagName << 1);
        writer.Write((byte)nameValue);
        writer.Write((byte)(nameValue >> 8));

        // Tag type: UINT16 (0x03)
        writer.Write((byte)0x03);

        // Value length
        writer.Write((byte)0x02);

        // Value
        writer.Write(value);
    }
}
