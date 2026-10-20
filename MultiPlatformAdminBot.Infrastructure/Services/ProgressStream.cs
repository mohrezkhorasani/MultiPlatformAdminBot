using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MultiPlatformAdminBot.Infrastructure.Services
{
    public class ProgressStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly IProgress<double> _progress;
        private readonly long _totalBytes;
        private long _bytesRead;

        public ProgressStream(Stream innerStream, IProgress<double> progress, long totalBytes)
        {
            _innerStream = innerStream;
            _progress = progress;
            _totalBytes = totalBytes;
            _bytesRead = 0;
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;
        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        public override void SetLength(long value) => _innerStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
            _bytesRead += count;
            if (_progress != null && _totalBytes > 0)
            {
                _progress.Report((double)_bytesRead / _totalBytes * 100);
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
            _bytesRead += count;
            if (_progress != null && _totalBytes > 0)
            {
                _progress.Report((double)_bytesRead / _totalBytes * 100);
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _innerStream.WriteAsync(buffer, cancellationToken);
            _bytesRead += buffer.Length;
            if (_progress != null && _totalBytes > 0)
            {
                _progress.Report((double)_bytesRead / _totalBytes * 100);
            }
        }
    }
}
