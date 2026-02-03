using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SlskDown.EMule
{
    /// <summary>
    /// Implementación del protocolo EC (External Connections) de aMule
    /// Basado en: https://wiki.amule.org/wiki/EC_Protocol_HOWTO
    /// </summary>
    
    public enum ECOpCode : byte
    {
        EC_OP_NOOP = 0x01,
        EC_OP_AUTH_REQ = 0x02,
        EC_OP_AUTH_FAIL = 0x03,
        EC_OP_AUTH_OK = 0x04,
        EC_OP_AUTH_SALT = 0x05,
        EC_OP_AUTH_PASSWD = 0x06,
        EC_OP_FAILED = 0x07,
        EC_OP_STRINGS = 0x08,
        EC_OP_MISC_DATA = 0x09,
        EC_OP_SHUTDOWN = 0x0A,
        EC_OP_ADD_LINK = 0x0B,
        EC_OP_STAT_REQ = 0x0C,
        EC_OP_GET_CONNSTATE = 0x0D,
        EC_OP_STATS = 0x0E,
        EC_OP_GET_DLOAD_QUEUE = 0x0F,
        EC_OP_GET_ULOAD_QUEUE = 0x10,
        EC_OP_SEARCH_START = 0x26,
        EC_OP_SEARCH_STOP = 0x27,
        EC_OP_SEARCH_RESULTS = 0x28,
        EC_OP_SEARCH_PROGRESS = 0x29,
        EC_OP_DOWNLOAD_SEARCH_RESULT = 0x2A,
        EC_OP_CONNECT = 0x4A,
        EC_OP_DISCONNECT = 0x4B,
        EC_OP_KAD_START = 0x48,
        EC_OP_KAD_STOP = 0x49
    }

    public enum ECTagName : ushort
    {
        EC_TAG_STRING = 0x0000,
        EC_TAG_PASSWD_HASH = 0x0002, // Corregido según documentación oficial
        EC_TAG_PROTOCOL_VERSION = 0x0004, // Corregido según captura Wireshark
        EC_TAG_CONNSTATE = 0x0005,
        EC_TAG_PASSWD_SALT = 0x0006,
        EC_TAG_VERSION_ID = 0x0018, // Corregido según captura Wireshark
        EC_TAG_DETAIL_LEVEL = 0x001A, // Corregido según captura Wireshark
        EC_TAG_CLIENT_NAME = 0x0100,
        EC_TAG_CLIENT_VERSION = 0x0101,
        EC_TAG_CLIENT_MOD = 0x0102,
        EC_TAG_SEARCH_TYPE = 0x0E03,
        EC_TAG_SEARCH_NAME = 0x0E04,
        EC_TAG_SEARCH_FILE_TYPE = 0x0E0A,
        EC_TAG_PARTFILE = 0x0400,
        EC_TAG_PARTFILE_NAME = 0x0401,
        EC_TAG_PARTFILE_SIZE_FULL = 0x0403,
        EC_TAG_PARTFILE_SIZE_DONE = 0x0404,
        EC_TAG_PARTFILE_SPEED = 0x0405,
        EC_TAG_PARTFILE_STATUS = 0x0406,
        EC_TAG_PARTFILE_PRIO = 0x0407,
        EC_TAG_PARTFILE_SOURCE_COUNT = 0x0408,
        EC_TAG_PARTFILE_SOURCE_COUNT_A4AF = 0x0409,
        EC_TAG_PARTFILE_SOURCE_COUNT_NOT_CURRENT = 0x040A,
        EC_TAG_PARTFILE_SOURCE_COUNT_XFER = 0x040B,
        EC_TAG_PARTFILE_ED2K_LINK = 0x040C,
        EC_TAG_PARTFILE_CAT = 0x040D,
        EC_TAG_PARTFILE_LAST_SEEN_COMP = 0x040E,
        EC_TAG_PARTFILE_PART_STATUS = 0x040F,
        EC_TAG_PARTFILE_GAP_STATUS = 0x0410,
        EC_TAG_PARTFILE_REQ_STATUS = 0x0411,
        EC_TAG_PARTFILE_SOURCE_NAMES = 0x0412,
        EC_TAG_PARTFILE_COMMENTS = 0x0413,
        EC_TAG_PARTFILE_STOPPED = 0x0414,
        EC_TAG_PARTFILE_DOWNLOAD_ACTIVE = 0x0415,
        EC_TAG_PARTFILE_LOST_CORRUPTION = 0x0416,
        EC_TAG_PARTFILE_GAINED_COMPRESSION = 0x0417,
        EC_TAG_PARTFILE_SAVED_ICH = 0x0418,
        EC_TAG_PARTFILE_SOURCE_NAMES_COUNTS = 0x0419,
        EC_TAG_PARTFILE_AVAILABLE_PARTS = 0x041A,
        EC_TAG_PARTFILE_SHARED = 0x041B,
        EC_TAG_PARTFILE_A4AF_AUTO = 0x041C,
        EC_TAG_PARTFILE_A4AF_SOURCES = 0x041D,
        EC_TAG_PARTFILE_HASH = 0x041E
    }

    public enum ECTagType : byte
    {
        EC_TAGTYPE_UNKNOWN = 0x00,
        EC_TAGTYPE_CUSTOM = 0x01,
        EC_TAGTYPE_UINT8 = 0x02,
        EC_TAGTYPE_UINT16 = 0x03,
        EC_TAGTYPE_UINT32 = 0x04,
        EC_TAGTYPE_UINT64 = 0x05,
        EC_TAGTYPE_STRING = 0x06,
        EC_TAGTYPE_DOUBLE = 0x07,
        EC_TAGTYPE_IPV4 = 0x08,
        EC_TAGTYPE_HASH16 = 0x09
    }

    [Flags]
    public enum ECFlags : uint
    {
        EC_FLAG_ZLIB = 0x00000001,
        EC_FLAG_ACCEPTS = 0x00000002,
        EC_FLAG_UTF8_NUMBERS = 0x00000020, // Corregido: era 0x02, debe ser 0x20
        EC_FLAG_UNKNOWN_MASK = 0xff7f7f08
    }

    public class ECPacket
    {
        public ECOpCode OpCode { get; set; }
        public List<ECTag> Tags { get; set; } = new List<ECTag>();

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Transmission layer: flags (BIG-ENDIAN)
                // aMule SIEMPRE espera UTF-8 numbers en los tags, independientemente de los flags
                // Usar EC_FLAG_UTF8_NUMBERS (0x20) para indicar que usamos UTF-8 numbers
                uint flags = (uint)ECFlags.EC_FLAG_UTF8_NUMBERS; // 0x20
                writer.Write((byte)(flags >> 24));
                writer.Write((byte)((flags >> 16) & 0xFF));
                writer.Write((byte)((flags >> 8) & 0xFF));
                writer.Write((byte)(flags & 0xFF));

                // Application layer
                using (var bodyMs = new MemoryStream())
                using (var bodyWriter = new BinaryWriter(bodyMs))
                {
                    // Con UTF-8 flag, usar UTF-8 numbers:
                    // OpCode (UTF-8 number)
                    WriteUTF8Number(bodyWriter, (uint)OpCode);

                    // Tag count (UTF-8 number)
                    WriteUTF8Number(bodyWriter, (uint)Tags.Count);

                    // Tags (formato UTF-8)
                    foreach (var tag in Tags)
                    {
                        tag.WriteTo(bodyWriter);
                    }

                    var body = bodyMs.ToArray();

                    // Transmission layer: body size (BIG-ENDIAN)
                    uint bodySize = (uint)body.Length;
                    writer.Write((byte)(bodySize >> 24));
                    writer.Write((byte)((bodySize >> 16) & 0xFF));
                    writer.Write((byte)((bodySize >> 8) & 0xFF));
                    writer.Write((byte)(bodySize & 0xFF));

                    // Body
                    writer.Write(body);
                }

                return ms.ToArray();
            }
        }

        public static ECPacket FromBytes(uint flags, byte[] body)
        {
            var packet = new ECPacket();

            using (var ms = new MemoryStream(body))
            using (var reader = new BinaryReader(ms))
            {
                bool hasUtf8Numbers = (flags & (uint)ECFlags.EC_FLAG_UTF8_NUMBERS) != 0;
                
                if (hasUtf8Numbers)
                {
                    // Con UTF-8 compression: [OpCode UTF-8] [tag count UTF-8] [tags...]
                    uint opCodeValue = ReadUTF8Number(reader);
                    packet.OpCode = (ECOpCode)opCodeValue;
                    
                    uint tagCount = ReadUTF8Number(reader);
                    for (int i = 0; i < tagCount; i++)
                    {
                        packet.Tags.Add(ECTag.ReadFrom(reader));
                    }
                }
                else
                {
                    // Sin UTF-8 compression (plain format): [OpCode 1 byte] [tag count 2 bytes] [tags...]
                    packet.OpCode = (ECOpCode)reader.ReadByte();
                    
                    // Tag count en big-endian (2 bytes)
                    ushort tagCount = (ushort)((reader.ReadByte() << 8) | reader.ReadByte());
                    
                    for (int i = 0; i < tagCount; i++)
                    {
                        packet.Tags.Add(ECTag.ReadFrom(reader));
                    }
                }
            }

            return packet;
        }

        private static void WriteUTF8Number(BinaryWriter writer, uint value)
        {
            // Codificación UTF-8 de números (ver protocolo EC)
            if (value < 0x80)
            {
                writer.Write((byte)value);
            }
            else if (value < 0x4000)
            {
                writer.Write((byte)(0x80 | (value >> 8)));
                writer.Write((byte)(value & 0xFF));
            }
            else if (value < 0x200000)
            {
                writer.Write((byte)(0xC0 | (value >> 16)));
                writer.Write((byte)((value >> 8) & 0xFF));
                writer.Write((byte)(value & 0xFF));
            }
            else if (value < 0x10000000)
            {
                writer.Write((byte)(0xE0 | (value >> 24)));
                writer.Write((byte)((value >> 16) & 0xFF));
                writer.Write((byte)((value >> 8) & 0xFF));
                writer.Write((byte)(value & 0xFF));
            }
            else
            {
                writer.Write((byte)0xF0);
                writer.Write((byte)((value >> 24) & 0xFF));
                writer.Write((byte)((value >> 16) & 0xFF));
                writer.Write((byte)((value >> 8) & 0xFF));
                writer.Write((byte)(value & 0xFF));
            }
        }

        private static uint ReadUTF8Number(BinaryReader reader)
        {
            byte first = reader.ReadByte();

            if ((first & 0x80) == 0)
            {
                // 1 byte: 0xxxxxxx
                return first;
            }
            else if ((first & 0xE0) == 0xC0)
            {
                // 2 bytes: 110xxxxx 10xxxxxx
                byte second = reader.ReadByte();
                return (uint)(((first & 0x1F) << 6) | (second & 0x3F));
            }
            else if ((first & 0xF0) == 0xE0)
            {
                // 3 bytes: 1110xxxx 10xxxxxx 10xxxxxx
                byte second = reader.ReadByte();
                byte third = reader.ReadByte();
                return (uint)(((first & 0x0F) << 12) | ((second & 0x3F) << 6) | (third & 0x3F));
            }
            else if ((first & 0xF8) == 0xF0)
            {
                // 4 bytes: 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
                byte second = reader.ReadByte();
                byte third = reader.ReadByte();
                byte fourth = reader.ReadByte();
                return (uint)(((first & 0x07) << 18) | ((second & 0x3F) << 12) | ((third & 0x3F) << 6) | (fourth & 0x3F));
            }
            else
            {
                // 5 bytes: 111110xx 10xxxxxx 10xxxxxx 10xxxxxx 10xxxxxx
                byte second = reader.ReadByte();
                byte third = reader.ReadByte();
                byte fourth = reader.ReadByte();
                byte fifth = reader.ReadByte();
                return (uint)(((first & 0x03) << 24) | ((second & 0x3F) << 18) | ((third & 0x3F) << 12) | ((fourth & 0x3F) << 6) | (fifth & 0x3F));
            }
        }
    }

    public class ECTag
    {
        public ECTagName Name { get; set; }
        public ECTagType Type { get; set; }
        public object Value { get; set; }
        public List<ECTag> SubTags { get; set; } = new List<ECTag>();

        public ECTag() { }

        public ECTag(ECTagName name, string value)
        {
            Name = name;
            Type = ECTagType.EC_TAGTYPE_STRING;
            Value = value;
        }

        public ECTag(ECTagName name, ushort value)
        {
            Name = name;
            Type = ECTagType.EC_TAGTYPE_UINT16;
            Value = value;
        }

        public ECTag(ECTagName name, byte[] hash)
        {
            Name = name;
            Type = ECTagType.EC_TAGTYPE_HASH16;
            Value = hash;
        }

        public void WriteToSimple(BinaryWriter writer)
        {
            // Formato simple (sin UTF-8) para AUTH_REQ
            // Tag name (2 bytes big-endian)
            ushort nameValue = (ushort)Name;
            writer.Write((byte)(nameValue >> 8));
            writer.Write((byte)(nameValue & 0xFF));

            // Tag type
            writer.Write((byte)Type);

            // Tag length + value
            using (var valueMs = new MemoryStream())
            using (var valueWriter = new BinaryWriter(valueMs))
            {
                // Write value
                WriteValue(valueWriter);

                var valueBytes = valueMs.ToArray();
                // Length (2 bytes big-endian)
                ushort length = (ushort)valueBytes.Length;
                writer.Write((byte)(length >> 8));
                writer.Write((byte)(length & 0xFF));
                writer.Write(valueBytes);
            }
        }

        public void WriteTo(BinaryWriter writer)
        {
            // Tag name (UTF-8 encoded)
            // IMPORTANTE: Solo los tags STRING tienen shift, los numéricos NO
            ushort nameValue = (ushort)Name;
            
            // Aplicar shift solo para tipos STRING
            if (Type == ECTagType.EC_TAGTYPE_STRING)
            {
                nameValue = (ushort)(nameValue << 1);
            }
            
            if (SubTags.Count > 0)
            {
                nameValue |= 1; // Set subtag flag
            }
            WriteUTF8Number(writer, nameValue);

            // Tag type
            writer.Write((byte)Type);

            // Tag length + value
            using (var valueMs = new MemoryStream())
            using (var valueWriter = new BinaryWriter(valueMs))
            {
                // Write subtags first
                if (SubTags.Count > 0)
                {
                    WriteUTF8Number(valueWriter, (uint)SubTags.Count);
                    foreach (var subTag in SubTags)
                    {
                        subTag.WriteTo(valueWriter);
                    }
                }

                // Write value
                WriteValue(valueWriter);

                var valueBytes = valueMs.ToArray();
                WriteUTF8Number(writer, (uint)valueBytes.Length);
                writer.Write(valueBytes);
            }
        }

        public static ECTag ReadFrom(BinaryReader reader)
        {
            var tag = new ECTag();

            // Read tag name
            uint nameValue = ReadUTF8Number(reader);
            bool hasSubTags = (nameValue & 1) == 1;
            tag.Name = (ECTagName)(nameValue >> 1);

            // Read tag type
            tag.Type = (ECTagType)reader.ReadByte();

            // Read tag length
            uint length = ReadUTF8Number(reader);

            // Read subtags if present
            if (hasSubTags)
            {
                uint subTagCount = ReadUTF8Number(reader);
                for (int i = 0; i < subTagCount; i++)
                {
                    tag.SubTags.Add(ReadFrom(reader));
                }
            }

            // Read value
            tag.ReadValue(reader, length);

            return tag;
        }

        private void WriteValue(BinaryWriter writer)
        {
            switch (Type)
            {
                case ECTagType.EC_TAGTYPE_STRING:
                    var str = (string)Value;
                    writer.Write(Encoding.UTF8.GetBytes(str));
                    writer.Write((byte)0); // Null terminator
                    break;

                case ECTagType.EC_TAGTYPE_UINT8:
                    writer.Write((byte)Value);
                    break;

                case ECTagType.EC_TAGTYPE_UINT16:
                    // BIG-ENDIAN según captura Wireshark
                    ushort val16 = (ushort)Value;
                    writer.Write((byte)(val16 >> 8));
                    writer.Write((byte)(val16 & 0xFF));
                    break;

                case ECTagType.EC_TAGTYPE_UINT32:
                    // BIG-ENDIAN
                    uint val32 = (uint)Value;
                    writer.Write((byte)(val32 >> 24));
                    writer.Write((byte)((val32 >> 16) & 0xFF));
                    writer.Write((byte)((val32 >> 8) & 0xFF));
                    writer.Write((byte)(val32 & 0xFF));
                    break;

                case ECTagType.EC_TAGTYPE_UINT64:
                    // BIG-ENDIAN
                    ulong val64 = (ulong)Value;
                    writer.Write((byte)(val64 >> 56));
                    writer.Write((byte)((val64 >> 48) & 0xFF));
                    writer.Write((byte)((val64 >> 40) & 0xFF));
                    writer.Write((byte)((val64 >> 32) & 0xFF));
                    writer.Write((byte)((val64 >> 24) & 0xFF));
                    writer.Write((byte)((val64 >> 16) & 0xFF));
                    writer.Write((byte)((val64 >> 8) & 0xFF));
                    writer.Write((byte)(val64 & 0xFF));
                    break;

                case ECTagType.EC_TAGTYPE_HASH16:
                    writer.Write((byte[])Value);
                    break;

                default:
                    throw new NotImplementedException($"Tipo de tag no implementado: {Type}");
            }
        }

        private void ReadValue(BinaryReader reader, uint length)
        {
            switch (Type)
            {
                case ECTagType.EC_TAGTYPE_STRING:
                    var bytes = reader.ReadBytes((int)length);
                    Value = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    break;

                case ECTagType.EC_TAGTYPE_UINT8:
                    Value = reader.ReadByte();
                    break;

                case ECTagType.EC_TAGTYPE_UINT16:
                    Value = reader.ReadUInt16();
                    break;

                case ECTagType.EC_TAGTYPE_UINT32:
                    Value = reader.ReadUInt32();
                    break;

                case ECTagType.EC_TAGTYPE_UINT64:
                    Value = reader.ReadUInt64();
                    break;

                case ECTagType.EC_TAGTYPE_HASH16:
                    Value = reader.ReadBytes(16);
                    break;

                default:
                    // Skip unknown types
                    reader.ReadBytes((int)length);
                    break;
            }
        }

        private static void WriteUTF8Number(BinaryWriter writer, uint value)
        {
            // UTF-8 encoding estándar aplicado a números
            // Verificado con captura Wireshark: 0x0200 → c8 80, 0x0202 → c8 82
            
            if (value < 0x80)
            {
                // 1 byte: 0xxxxxxx
                writer.Write((byte)value);
            }
            else if (value < 0x800)
            {
                // 2 bytes: 110xxxxx 10xxxxxx
                writer.Write((byte)((value >> 6) | 0xC0));
                writer.Write((byte)((value & 0x3F) | 0x80));
            }
            else if (value < 0x10000)
            {
                // 3 bytes: 1110xxxx 10xxxxxx 10xxxxxx
                writer.Write((byte)((value >> 12) | 0xE0));
                writer.Write((byte)(((value >> 6) & 0x3F) | 0x80));
                writer.Write((byte)((value & 0x3F) | 0x80));
            }
            else if (value < 0x200000)
            {
                // 4 bytes: 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
                writer.Write((byte)((value >> 18) | 0xF0));
                writer.Write((byte)(((value >> 12) & 0x3F) | 0x80));
                writer.Write((byte)(((value >> 6) & 0x3F) | 0x80));
                writer.Write((byte)((value & 0x3F) | 0x80));
            }
            else
            {
                // 5 bytes: 111110xx 10xxxxxx 10xxxxxx 10xxxxxx 10xxxxxx
                writer.Write((byte)((value >> 24) | 0xF8));
                writer.Write((byte)(((value >> 18) & 0x3F) | 0x80));
                writer.Write((byte)(((value >> 12) & 0x3F) | 0x80));
                writer.Write((byte)(((value >> 6) & 0x3F) | 0x80));
                writer.Write((byte)((value & 0x3F) | 0x80));
            }
        }

        private static uint ReadUTF8Number(BinaryReader reader)
        {
            byte first = reader.ReadByte();

            if ((first & 0x80) == 0)
            {
                return first;
            }
            else if ((first & 0xC0) == 0x80)
            {
                return (uint)(((first & 0x3F) << 8) | reader.ReadByte());
            }
            else if ((first & 0xE0) == 0xC0)
            {
                return (uint)(((first & 0x1F) << 16) | (reader.ReadByte() << 8) | reader.ReadByte());
            }
            else if ((first & 0xF0) == 0xE0)
            {
                return (uint)(((first & 0x0F) << 24) | (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte());
            }
            else
            {
                return (uint)((reader.ReadByte() << 24) | (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte());
            }
        }

        /// <summary>
        /// Obtiene un subtag por nombre
        /// </summary>
        public ECTag GetSubTag(ECTagName name)
        {
            return SubTags?.FirstOrDefault(t => t.Name == name);
        }

        /// <summary>
        /// Valor como string
        /// </summary>
        public string StringValue => Value?.ToString();

        /// <summary>
        /// Valor como UInt64
        /// </summary>
        public ulong? UInt64Value
        {
            get
            {
                if (Value == null) return null;
                if (Value is ulong ul) return ul;
                if (ulong.TryParse(Value.ToString(), out var result)) return result;
                return null;
            }
        }

        /// <summary>
        /// Valor como UInt32
        /// </summary>
        public uint? UInt32Value
        {
            get
            {
                if (Value == null) return null;
                if (Value is uint ui) return ui;
                if (uint.TryParse(Value.ToString(), out var result)) return result;
                return null;
            }
        }
    }
}
