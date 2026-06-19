using PBP.Core.Models;

namespace PBP.Core.Services;

public static class SFOStreamExtensions
{
    public static void WriteSFO(this Stream stream, SFOData sfo)
    {
        stream.WriteUInt32(sfo.Magic, 1);
        stream.WriteUInt32(sfo.Version, 1);
        stream.WriteUInt32(sfo.KeyTableOffset, 1);
        stream.WriteUInt32(sfo.DataTableOffset, 1);
        stream.WriteInt32((ushort)sfo.Entries.Count, 1);

        for (var i = 0; i < sfo.Entries.Count; i++)
        {
            var entry = sfo.Entries[i];
            stream.WriteUInt16(entry.KeyOffset, 1);
            stream.WriteUInt16(entry.Format, 1);
            stream.WriteUInt32(entry.Length, 1);
            stream.WriteUInt32(entry.MaxLength, 1);
            stream.WriteUInt32(entry.DataOffset, 1);
        }

        for (var i = 0; i < sfo.Entries.Count; i++)
        {
            var key = sfo.Entries[i].Key;
            stream.Write(key, 0, key.Length);
            stream.WriteByte(0);
        }

        for (var i = 0; i < sfo.Padding; i++)
            stream.WriteByte(0);

        for (var i = 0; i < sfo.Entries.Count; i++)
        {
            var entry = sfo.Entries[i];
            var value = entry.Value;

            switch (entry.Format)
            {
                case 0x0204:
                    stream.Write((string)value!, 0, ((string)value!).Length);
                    stream.WriteByte(0);

                    for (var j = 0; j < entry.MaxLength - entry.Length; j++)
                        stream.WriteByte(0);

                    break;
                case 0x0404:
                    stream.WriteInt32(Convert.ToInt32(value), 1);
                    break;
            }
        }
    }

    public static void Write(this Stream stream, string buffer, int offset, int size)
    {
        var membuf = System.Text.Encoding.ASCII.GetBytes(buffer);

        stream.Write(membuf, offset, size);
    }

    public static void WriteUInt16(this Stream stream, ushort value, int count)
    {
        var p = 0;

        while (p < count)
        {
            stream.Write(BitConverter.GetBytes(value), 0, sizeof(ushort));
            p += 1;
        }
    }

    public static void WriteUInt32(this Stream stream, uint value, int count)
    {
        var p = 0;

        while (p < count)
        {
            stream.Write(BitConverter.GetBytes(value), 0, sizeof(uint));
            p += 1;
        }
    }

    public static void WriteInt32(this Stream stream, int value, int count)
    {
        var p = 0;

        while (p < count)
        {
            stream.Write(BitConverter.GetBytes(value), 0, sizeof(int));
            p += 1;
        }
    }
}