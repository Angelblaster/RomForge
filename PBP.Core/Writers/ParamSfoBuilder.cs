using System.Text;

namespace PBP.Core.Writers;

/// <summary>
/// PARAM.SFO 빌더
/// PS1 PBP용 최소 필수 항목만 구현
/// </summary>
public class ParamSfoBuilder
{
    // SFO 매직
    private static readonly byte[] Magic = [0x00, 0x50, 0x53, 0x46]; // \x00PSF
    private const uint Version = 0x00000101;

    private readonly List<SfoEntry> _entries = [];

    // ─── 편의 프로퍼티 ───

    public string Title
    {
        set => Set("TITLE", value, SfoType.Utf8, 128);
    }

    public string DiscId
    {
        set => Set("DISC_ID", value, SfoType.Utf8, 16);
    }

    public string DiscVersion
    {
        set => Set("DISC_VERSION", value, SfoType.Utf8, 8);
    }

    public int ParentalLevel
    {
        set => Set("PARENTAL_LEVEL", value);
    }

    public int Bootable
    {
        set => Set("BOOTABLE", value);
    }

    public ParamSfoBuilder()
    {
        // PS1 PBP 기본값
        Set("BOOTABLE", 1);
        Set("CATEGORY", "ME", SfoType.Utf8, 4);  // ME = PS1 게임
        Set("DISC_VERSION", "1.00", SfoType.Utf8, 8);
        Set("PARENTAL_LEVEL", 1);
    }

    public ParamSfoBuilder WithTitle(string title) { Title = title; return this; }
    public ParamSfoBuilder WithDiscId(string discId) { DiscId = discId; return this; }
    public ParamSfoBuilder WithDiscVersion(string version) { DiscVersion = version; return this; }
    public ParamSfoBuilder WithParentalLevel(int level) { ParentalLevel = level; return this; }

    public byte[] Build()
    {
        // SFO 스펙: 키 알파벳 오름차순 정렬 필수
        var sorted = _entries.OrderBy(e => e.Key, StringComparer.Ordinal).ToList();

        int keyTableSize = sorted.Sum(e => Encoding.UTF8.GetByteCount(e.Key) + 1);
        // 4바이트 정렬
        keyTableSize = (keyTableSize + 3) & ~3;

        int dataTableSize = sorted.Sum(e => e.MaxLen);

        // 헤더: 20바이트
        // 인덱스 테이블: 16바이트 * 항목 수
        int headerSize = 20;
        int indexTableSize = 16 * sorted.Count;
        int keyTableOffset = headerSize + indexTableSize;
        int dataTableOffset = keyTableOffset + keyTableSize;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        // ─── 헤더 ───
        w.Write(Magic);                          // [0x00] magic
        w.Write(Version);                        // [0x04] version
        w.Write((uint)keyTableOffset);           // [0x08] key table offset
        w.Write((uint)dataTableOffset);          // [0x0C] data table offset
        w.Write((uint)sorted.Count);             // [0x10] entry count

        // ─── 인덱스 테이블 ───
        int keyOffset = 0;
        int dataOffset = 0;

        foreach (var entry in sorted)
        {
            w.Write((ushort)keyOffset);          // key offset
            w.Write(entry.Format);         // format (4=utf8, 5=int32)
            w.Write((byte)entry.Type);           // type
            w.Write((uint)entry.DataLen);        // data length
            w.Write((uint)entry.MaxLen);         // max length
            w.Write((uint)dataOffset);           // data offset

            keyOffset += Encoding.UTF8.GetByteCount(entry.Key) + 1;
            dataOffset += entry.MaxLen;
        }

        // ─── 키 테이블 ───
        long keyStart = ms.Position;
        foreach (var entry in sorted)
        {
            w.Write(Encoding.UTF8.GetBytes(entry.Key));
            w.Write((byte)0); // null terminator
        }
        // 패딩
        long keyEnd = ms.Position;
        int keyPad = keyTableOffset + keyTableSize - (int)(keyEnd - 0); // 절대 오프셋 기준
        // 현재 위치에서 dataTableOffset까지 패딩
        long padTo = dataTableOffset;
        while (ms.Position < padTo) w.Write((byte)0);

        // ─── 데이터 테이블 ───
        foreach (var entry in sorted)
        {
            if (entry.Type == SfoType.Int32)
            {
                w.Write((int)entry.Data!);
                // MaxLen은 항상 4
            }
            else
            {
                var strBytes = Encoding.UTF8.GetBytes((string)entry.Data!);
                w.Write(strBytes);
                w.Write((byte)0); // null terminator
                // MaxLen까지 패딩
                int written = strBytes.Length + 1;
                while (written < entry.MaxLen) { w.Write((byte)0); written++; }
            }
        }

        return ms.ToArray();
    }

    private void Set(string key, string value, SfoType type, int maxLen)
    {
        var bytes = Encoding.UTF8.GetByteCount(value) + 1;
        var existing = _entries.FirstOrDefault(e => e.Key == key);
        if (existing != null) _entries.Remove(existing);

        _entries.Add(new SfoEntry
        {
            Key = key,
            Type = type,
            Format = type == SfoType.Int32 ? (ushort)0x0404 : (ushort)0x0204,
            Data = value,
            DataLen = bytes,
            MaxLen = maxLen
        });
    }

    private void Set(string key, int value)
    {
        var existing = _entries.FirstOrDefault(e => e.Key == key);
        if (existing != null) _entries.Remove(existing);

        _entries.Add(new SfoEntry
        {
            Key = key,
            Type = SfoType.Int32,
            Format = 0x0404,
            Data = value,
            DataLen = 4,
            MaxLen = 4
        });
    }

    private enum SfoType : byte
    {
        Utf8 = 0x02,
        Int32 = 0x04
    }

    private class SfoEntry
    {
        public string Key { get; set; } = string.Empty;
        public SfoType Type { get; set; }
        public ushort Format { get; set; }
        public object? Data { get; set; }
        public int DataLen { get; set; }
        public int MaxLen { get; set; }
    }
}
