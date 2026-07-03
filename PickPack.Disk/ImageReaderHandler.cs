using Ionic.Zip;
using Ionic.Zlib;
using PickPack.Disk.ETC;
using System.Threading.Channels;

namespace PickPack.Disk
{
    public interface IImageReaderHandler
    {
        Task WriteImageAsync(Stream sourceStream, string outputPath, long totalSize, CancellationToken ct);
    }

    public class ZipReadHandler(long maxOutputSegmentSize, CompressionLevel compressionLevel, Action<long, long, string> progressReporter, CancellationToken ct) : IImageReaderHandler
    {
        #region Constructor

        #endregion

        public async Task WriteImageAsync(Stream sourceStream, string outputPath, long totalSize, CancellationToken ct)
        {
            using var zipFile = new ZipFile();

            zipFile.UseZip64WhenSaving = Zip64Option.AsNecessary;
            zipFile.CompressionLevel = compressionLevel;
            zipFile.MaxOutputSegmentSize64 = maxOutputSegmentSize;
            zipFile.BufferSize = Optimal.BufferSize;
            zipFile.CodecBufferSize = Optimal.BufferSize;            
            zipFile.ParallelDeflateThreshold = 50 * 1024 * 1024;
            zipFile.AlternateEncoding = null;
            zipFile.AlternateEncodingUsage = ZipOption.Never;
            zipFile.Strategy = CompressionStrategy.Default;

            string imgFileName = $"{Path.GetFileNameWithoutExtension(outputPath)}.img";
            var optimizedStream = new DirectReadStream(sourceStream as FileStream, totalSize);

            zipFile.SaveProgress += (sender, e) => OnZipSaveProgress(e);
            zipFile.AddEntry(imgFileName, optimizedStream);
            
            await Task.Run(() => zipFile.Save(outputPath), ct);
        }

        private void OnZipSaveProgress(SaveProgressEventArgs e)
        {            
            if (ct.IsCancellationRequested)
                e.Cancel = true;
            else
            {
                if (e.EventType == ZipProgressEventType.Saving_EntryBytesRead && e.TotalBytesToTransfer > 0)
                    progressReporter(e.BytesTransferred, e.TotalBytesToTransfer, "이미지 저장 중...");
            }
        }

        #region Inner Class

        private class DirectReadStream(Stream baseStream, long length) : Stream
        {
            long _position = 0;
            bool _disposed = false;

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => length;

            public override int Read(byte[] buffer, int offset, int count)
            {
                long remaining = length - _position;

                if (remaining <= 0) 
                    return 0;

                int bytesToRead = (int)Math.Min(count, remaining);

                try
                {
                    int bytesRead = baseStream.Read(buffer, offset, bytesToRead);

                    _position += bytesRead;

                    return bytesRead;
                }
                catch (IOException)
                {
                    Array.Clear(buffer, offset, bytesToRead);
                    _position += bytesToRead;

                    return bytesToRead;
                }
            }

            public override long Position
            {
                get => _position;
                set => Seek(value, SeekOrigin.Begin);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        _position = offset;
                        break;

                    case SeekOrigin.Current:
                        _position += offset;
                        break;

                    case SeekOrigin.End:
                        _position = length + offset;
                        break;
                }

                baseStream.Seek(_position, SeekOrigin.Begin);
                return _position;
            }

            protected override void Dispose(bool disposing)
            {
                if (!_disposed && disposing)
                    _disposed = true;

                base.Dispose(disposing);
            }

            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Flush() => baseStream.Flush();
        }

        #endregion
    }

    public class RawImageReadHandler(Action<long, long, string> progressReporter) : IImageReaderHandler
    {

        #region Field & Const

        const int ERROR_CRC = unchecked((int)0x80070017);
        const int ERROR_SECTOR_NOT_FOUND = unchecked((int)0x8007001B);

        #endregion
        #region Constructor

        #endregion

        public async Task WriteImageAsync(Stream sourceStream, string outputPath, long totalSize, CancellationToken ct)
        {
            var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(Optimal.ChannelCapacity) 
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            var producerTask = ProduceDataAsync(sourceStream, channel.Writer, totalSize, ct);
            var consumerTask = ConsumeDataAsync(channel.Reader, outputPath, totalSize, ct);

            await consumerTask;
            await producerTask;
        }

        private static async Task ProduceDataAsync(Stream sourceStream, ChannelWriter<byte[]> writer, long totalSize, CancellationToken ct)
        {
            byte[] rentedBuffer = Optimal.ArrayPool.Rent(Optimal.BufferSize);
            long totalRead = 0;

            try
            {
                while (totalRead < totalSize)
                {
                    ct.ThrowIfCancellationRequested();

                    int read;

                    try
                    {
                        read = await sourceStream.ReadAsync(rentedBuffer.AsMemory(0, Optimal.BufferSize), ct);

                        if (read == 0) 
                            break;
                    }
                    catch (IOException ex) when (ex.HResult == ERROR_CRC || ex.HResult == ERROR_SECTOR_NOT_FOUND)
                    {
                        Array.Clear(rentedBuffer, 0, Optimal.BufferSize);
                        read = Optimal.BufferSize;
                    }

                    if (read == Optimal.BufferSize)
                    {
                        await writer.WriteAsync(rentedBuffer, ct);
                        rentedBuffer = Optimal.ArrayPool.Rent(Optimal.BufferSize);
                    }
                    else
                    {
                        byte[] dataToSend = Optimal.ArrayPool.Rent(read);
                        Array.Copy(rentedBuffer, dataToSend, read);

                        await writer.WriteAsync(dataToSend, ct);
                    }

                    totalRead += read;
                }

                if (totalSize > totalRead)
                    await writer.WriteAsync([], ct);
            }
            finally
            {
                Optimal.ArrayPool.Return(rentedBuffer);
                writer.Complete();
            }
        }

        private async Task ConsumeDataAsync(ChannelReader<byte[]> reader, string outputPath, long totalSize, CancellationToken ct)
        {
            long totalWritten = 0;
            long zeroBytesWritten = 0;
            using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, Optimal.BufferSize, FileOptions.Asynchronous);

            await foreach (var buffer in reader.ReadAllAsync(ct))
            {
                try
                {
                    if (buffer.Length == 0)
                    {
                        long bytesRemaining = totalSize - totalWritten;

                        while (bytesRemaining > 0)
                        {
                            int writeCount = (int)Math.Min(Optimal.BufferSize, bytesRemaining);

                            await outStream.WriteAsync(Optimal.ZeroBuffer.AsMemory(0, writeCount), ct);

                            bytesRemaining -= writeCount;
                            zeroBytesWritten += writeCount;
                            progressReporter(totalWritten + zeroBytesWritten, totalSize, "이미지 저장 중...");
                        }
                    }
                    else
                    {
                        await outStream.WriteAsync(buffer, ct);

                        totalWritten += buffer.Length;
                        progressReporter(totalWritten, totalSize, "이미지 저장 중...");
                    }
                }
                finally
                {
                    if (buffer != Array.Empty<byte>())
                        Optimal.ArrayPool.Return(buffer);
                }
            }
        }
    }
}