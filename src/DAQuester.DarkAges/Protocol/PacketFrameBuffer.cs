#nullable disable
// Extracted from local Excalibur commit 7d11dfe91e680de9a9b824137caa301f5429f03c.
using System;

namespace DAQuester.DarkAges.Protocol
{
    public sealed class PacketFrameBuffer
    {
        private const byte Signature = 0xAA;
        private byte[] buffer;
        private readonly int maximumFrameLength;
        private readonly int maximumDiscardedBytes;
        private readonly int maximumConsecutiveFramingErrors;
        private int start;
        private int count;
        private int discardedByteCount;
        private int discardedBytesSinceValidFrame;
        private int consecutiveFramingErrors;
        public PacketFrameBuffer() : this(4096, ushort.MaxValue + 3)
        {
        }

        public PacketFrameBuffer(int initialCapacity) : this(initialCapacity, ushort.MaxValue + 3)
        {
        }

        public PacketFrameBuffer(int initialCapacity, int maximumFrameLength) : this(initialCapacity, maximumFrameLength, 4096, 8)
        {
        }

        public PacketFrameBuffer(int initialCapacity, int maximumFrameLength, int maximumDiscardedBytes, int maximumConsecutiveFramingErrors)
        {
            if (maximumFrameLength < 4)
                throw new ArgumentOutOfRangeException(nameof(maximumFrameLength));
            if (maximumDiscardedBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumDiscardedBytes));
            if (maximumConsecutiveFramingErrors < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumConsecutiveFramingErrors));
            this.buffer = new byte[Math.Max(4, initialCapacity)];
            this.maximumFrameLength = maximumFrameLength;
            this.maximumDiscardedBytes = maximumDiscardedBytes;
            this.maximumConsecutiveFramingErrors = maximumConsecutiveFramingErrors;
        }

        public int Count
        {
            get
            {
                return this.count;
            }
        }

        public bool FirstByteIsSignature
        {
            get
            {
                return this.count == 0 || this.buffer[this.start] == Signature;
            }
        }

        public byte FirstByte
        {
            get
            {
                return this.count > 0 ? this.buffer[this.start] : (byte)0;
            }
        }

        public int DiscardedByteCount
        {
            get
            {
                return this.discardedByteCount;
            }
        }

        public bool RecoveryRequired
        {
            get
            {
                return this.discardedBytesSinceValidFrame >= this.maximumDiscardedBytes || this.consecutiveFramingErrors >= this.maximumConsecutiveFramingErrors;
            }
        }

        public void Append(byte[] source, int offset, int length)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (length <= 0)
                return;
            this.EnsureWritable(length);
            Buffer.BlockCopy(source, offset, this.buffer, this.start + this.count, length);
            this.count += length;
        }

        public bool TryReadFrame(out byte[] frame)
        {
            frame = null;
            while (true)
            {
                if (!this.FirstByteIsSignature)
                {
                    int signatureOffset = this.FindNextSignatureOffset();
                    if (signatureOffset < 0)
                    {
                        this.DiscardPrefix(this.count);
                        return false;
                    }

                    this.DiscardPrefix(signatureOffset);
                }

                if (this.count <= 3)
                    return false;
                int frameLength = ((int)this.buffer[this.start + 1] << 8) + this.buffer[this.start + 2] + 3;
                if (frameLength > this.maximumFrameLength)
                {
                    int signatureOffset = this.FindNextSignatureOffset();
                    if (signatureOffset < 0)
                        return false;
                    this.DiscardPrefix(signatureOffset);
                    continue;
                }

                if (frameLength > this.count)
                    return false;
                frame = new byte[frameLength];
                Buffer.BlockCopy(this.buffer, this.start, frame, 0, frameLength);
                this.Consume(frameLength);
                this.discardedBytesSinceValidFrame = 0;
                this.consecutiveFramingErrors = 0;
                return true;
            }
        }

        private void DiscardPrefix(int length)
        {
            this.Consume(length);
            this.discardedByteCount += length;
            this.discardedBytesSinceValidFrame += length;
            this.consecutiveFramingErrors++;
        }

        private int FindNextSignatureOffset()
        {
            for (int offset = 1; offset < this.count; offset++)
            {
                if (this.buffer[this.start + offset] == Signature)
                    return offset;
            }

            return -1;
        }

        private void EnsureWritable(int additionalLength)
        {
            int required = this.count + additionalLength;
            if (required > this.buffer.Length)
            {
                int capacity = this.buffer.Length;
                while (capacity < required)
                    capacity *= 2;
                byte[] next = new byte[capacity];
                if (this.count > 0)
                    Buffer.BlockCopy(this.buffer, this.start, next, 0, this.count);
                this.buffer = next;
                this.start = 0;
                return;
            }

            if (this.start + this.count + additionalLength > this.buffer.Length)
            {
                Buffer.BlockCopy(this.buffer, this.start, this.buffer, 0, this.count);
                this.start = 0;
            }
        }

        private void Consume(int length)
        {
            this.start += length;
            this.count -= length;
            if (this.count == 0)
            {
                this.start = 0;
            }
            else if (this.start > this.buffer.Length / 2)
            {
                Buffer.BlockCopy(this.buffer, this.start, this.buffer, 0, this.count);
                this.start = 0;
            }
        }
    }
}
