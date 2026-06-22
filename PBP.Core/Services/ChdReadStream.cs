using CHD.Core.Interop;

namespace PBP.Core.Services;

public class ChdReadStream(LibChdrWrapper wrapper, long totalLength) : Stream
{
    private long _position;
    private byte[]? _currentHunk;
    private uint _cachedHunkIndex = uint.MaxValue;
    private readonly uint _sectorsPerHunk = (wrapper.Header?.hunkbytes ?? 0) / 2448u;

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;

        while (bytesRead < count && _position < totalLength)
        {
            uint sectorIdx = (uint)(_position / 2352);
            int posInSector = (int)(_position % 2352);

            uint hunkIdx = sectorIdx / _sectorsPerHunk;
            int hunkOffset = (int)(sectorIdx % _sectorsPerHunk) * 2448;

            if (_cachedHunkIndex != hunkIdx)
            {
                _currentHunk = wrapper.ReadHunk(hunkIdx);
                _cachedHunkIndex = hunkIdx;
            }

            int toRead = (int)Math.Min(count - bytesRead, 2352 - posInSector);
            toRead = (int)Math.Min(toRead, totalLength - _position);

            if (_currentHunk == null) throw new NullReferenceException(nameof(_currentHunk));

            Array.Copy(_currentHunk, hunkOffset + posInSector, buffer, offset + bytesRead, toRead);
            bytesRead += toRead;
            _position += toRead;
        }
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => totalLength + offset,
            _ => _position
        };
        _position = Math.Clamp(_position, 0, totalLength);

        return _position;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => totalLength;
    public override long Position { get => _position; set => _position = value; }
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}