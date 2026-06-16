using CHD.Core.Interop;
using CHD.Core.Interop.Enums;
using CHD.Core.Services;
using DiscUtils.Iso9660;
using PBP.Core.Models;
using System.Text.RegularExpressions;

namespace PBP.Core.Cue;

public static class GameIdReader
{
    private const string Fallback = "SLUS00000";

    private static readonly Regex BootRegex = new(@"BOOT\s*=\s*(.*)", RegexOptions.IgnoreCase);
    private static readonly Regex SerialRegex = new(@"([A-Z]+)_?(\d+)\.(\d+)", RegexOptions.IgnoreCase);

    public static string ReadFromDisk(DiskSource source)
    {
        try
        {
            return source.Type == DiskSourceType.Chd
                ? ReadFromChd(source.FilePath)
                : ReadFromStream(source);
        }
        catch
        {
            return Fallback;
        }
    }

    private static string ReadFromChd(string filePath)
    {
        using var wrapper = new LibChdrWrapper();
        wrapper.Open(filePath, ChdrOpenFlags.CHDOPEN_READ);
        return new PS1GameIdExtractor(wrapper).ExtractGameId() ?? Fallback;
    }

    private static string ReadFromStream(DiskSource source)
    {
        using var baseStream = new FileStream(source.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        Stream isoStream = baseStream.Length >= 2352 && baseStream.Length % 2352 == 0
            ? new RawToCookedStream(baseStream)
            : baseStream;

        using var cdReader = new CDReader(isoStream, false);
        var systemCnf = cdReader.Root.GetFiles()
            .FirstOrDefault(f => f.Name.StartsWith("SYSTEM.CNF", StringComparison.OrdinalIgnoreCase));

        if (systemCnf == null) return Fallback;

        using var reader = new StreamReader(systemCnf.OpenRead());
        var bootMatch = BootRegex.Match(reader.ReadToEnd());
        if (!bootMatch.Success) return Fallback;

        var serialMatch = SerialRegex.Match(bootMatch.Groups[1].Value);
        if (!serialMatch.Success) return Fallback;

        return string.Concat(
            serialMatch.Groups[1].Value,
            serialMatch.Groups[2].Value,
            serialMatch.Groups[3].Value).ToUpperInvariant();
    }

    private class RawToCookedStream(Stream baseStream) : Stream
    {
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => (baseStream.Length / 2352) * 2048;
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
                long sector = (_position + totalRead) / 2048;
                int inSector = (int)((_position + totalRead) % 2048);
                baseStream.Position = sector * 2352 + 24 + inSector;

                int toRead = Math.Min(count - totalRead, 2048 - inSector);
                int read = baseStream.Read(buffer, offset + totalRead, toRead);
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
                SeekOrigin.End => Length + offset,
                _ => _position,
            };
            return _position = Math.Clamp(target, 0, Length);
        }

        public override void Flush() => baseStream.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}