using DiscUtils.Iso9660;
using DiscUtils.Streams;
using PBP.Core.Models;
using PBP.Core.Readers;
using System.Text.RegularExpressions;

namespace PBP.Core.Cue;

public static class GameIdReader
{
    private static readonly Regex BootRegex = new(
        @"BOOT\s*=\s*(.*)",
        RegexOptions.IgnoreCase);

    private static readonly Regex SerialRegex = new(
        @"([A-Z]+)_?(\d+)\.(\d+)",
        RegexOptions.IgnoreCase);

    public static string ReadFromDisk(DiskSource source)
    {
        string isoPath = source.FilePath;
        string? tempPath = null;

        if (source.Type == DiskSourceType.Chd)
        {
            tempPath = ExtractChdToTemp(source);
            isoPath = tempPath;
        }

        try
        {
            return FindGameId(isoPath) ?? "SLUS00000";
        }
        finally
        {
            if (tempPath != null && File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static string? FindGameId(string isoPath)
    {
        var fileStream = new FileStream(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Stream stream = (new FileInfo(isoPath).Length % 2352 == 0) ? new RawToCookedStream(fileStream) : fileStream;

        using (stream)
        using (var cdReader = new CDReader(stream, false))
        {
            foreach (var file in cdReader.Root.GetFiles())
            {
                if (!file.Name.Contains("SYSTEM.CNF", StringComparison.OrdinalIgnoreCase)) continue;
                using var reader = new StreamReader(file.OpenRead());
                var match = BootRegex.Match(reader.ReadToEnd());
                if (match.Success)
                {
                    var s = SerialRegex.Match(match.Groups[1].Value);
                    if (s.Success) return $"{s.Groups[1].Value}{s.Groups[2].Value}{s.Groups[3].Value}".ToUpper();
                }
            }
        }
        return null;
    }

    private static string ExtractChdToTemp(DiskSource source)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"pbp_tmp_{Guid.NewGuid():N}.bin");
        using var reader = DiskReaderFactory.Create(source);

        using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
        const int batch = 64;
        for (long s = 0; s < reader.TotalSectors; s += batch)
        {
            int count = (int)Math.Min(batch, reader.TotalSectors - s);
            var data = reader.ReadSectors(s, count);
            fs.Write(data, 0, data.Length);
        }

        return tempPath;
    }

    private class RawToCookedStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _length;
        private long _position;

        public RawToCookedStream(Stream baseStream)
        {
            _baseStream = baseStream;
            _length = (baseStream.Length / 2352) * 2048;
            _position = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                long currentSector = (_position + totalRead) / 2048;
                int offsetInSector = (int)((_position + totalRead) % 2048);

                _baseStream.Position = (currentSector * 2352) + 24 + offsetInSector;

                int canReadInSector = 2048 - offsetInSector;
                int toRead = Math.Min(count - totalRead, canReadInSector);

                int read = _baseStream.Read(buffer, offset + totalRead, toRead);
                if (read == 0) break;
                totalRead += read;
            }
            _position += totalRead;
            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => _position
            };
            _position = Math.Clamp(target, 0, _length);
            return _position;
        }

        public override void Flush() => _baseStream.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}