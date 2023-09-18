using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MultiServerIntegrateBot.Common
{
    /// <summary>
    /// 
    /// </summary>
    public class MemoryQueue
    {
        /// <summary>
        /// 
        /// </summary>
        private static readonly CopyDelegate<IntPtr, byte[]> copyFromPointer = (src, sOff, dst, dOff, len) => Marshal.Copy(src + sOff, dst, dOff, len);
        /// <summary>
        /// 
        /// </summary>
        private static readonly CopyDelegate<byte[], IntPtr> copyToPointer = (src, sOff, dst, dOff, len) => Marshal.Copy(src, sOff, dst + dOff, len);
        /// <summary>
        /// 
        /// </summary>
        private static readonly CopyDelegate<Stream, byte[]> copyFromStream = (src, sOff, dst, dOff, len) => src.Read(dst, dOff, len);
        /// <summary>
        /// 
        /// </summary>
        private static readonly CopyDelegate<byte[], Stream> copyToStream = (src, sOff, dst, dOff, len) => dst.Write(src, sOff, len);

        /// <summary>
        /// 
        /// </summary>
        private int head;
        /// <summary>
        /// 
        /// </summary>
        private int tail;
        /// <summary>
        /// 
        /// </summary>
        private int length;
        /// <summary>
        /// 
        /// </summary>
        private byte[] buffer;

        /// <summary>
        /// 
        /// </summary>
        public int Length
        {
            get { return length; }
        }

        /// <summary>
        /// 
        /// </summary>
        public MemoryQueue() : this(2048)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public MemoryQueue(int capacity)
        {
            buffer = new byte[capacity];
        }

        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            head = 0;
            tail = 0;
            length = 0;
        }

        /// <summary>
        /// 
        /// </summary>
        private void EnsureCapacity(int capacity)
        {
            if (capacity > buffer.Length)
            {
                SetCapacity((capacity + 2047) & ~2047);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetCapacity(int capacity)
        {
            byte[] newBuffer = new byte[capacity];

            if (length > 0)
            {
                if (head < tail)
                {
                    Buffer.BlockCopy(buffer, head, newBuffer, 0, length);
                }
                else
                {
                    Buffer.BlockCopy(buffer, head, newBuffer, 0, buffer.Length - head);
                    Buffer.BlockCopy(buffer, 0, newBuffer, buffer.Length - head, tail);
                }
            }

            head = 0;
            tail = length;
            buffer = newBuffer;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Enqueue<T>(T source, int count, CopyDelegate<T, byte[]> copyFrom)
        {
            if (count == 0)
            {
                return;
            }

            lock (this)
            {
                EnsureCapacity(this.length + count);

                if (head < tail)
                {
                    int rightLength = buffer.Length - tail;

                    if (rightLength >= count)
                    {
                        copyFrom(source, 0, buffer, tail, count);
                    }
                    else
                    {
                        copyFrom(source, 0, buffer, tail, rightLength);
                        copyFrom(source, rightLength, buffer, 0, count - rightLength);
                    }
                }
                else
                {
                    copyFrom(source, 0, buffer, tail, count);
                }
                tail = (tail + count) % buffer.Length;
                this.length += count;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Enqueue(byte[] buffer, int offset, int count)
        {
            var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            Enqueue(pin.AddrOfPinnedObject() + offset, count, copyFromPointer);

            pin.Free();
        }

        /// <summary>
        /// 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(Stream stream, int count)
        {
            Enqueue(stream, count, copyFromStream);
        }

        /// <summary>
        /// 
        /// </summary>
        public int Dequeue<T>(T destination, int count, CopyDelegate<byte[], T> copyTo)
        {
            lock (this)
            {
                if (count > this.length)
                {
                    count = this.length;
                }

                if (count == 0)
                {
                    return 0;
                }

                if (head < tail)
                {
                    copyTo(buffer, head, destination, 0, count);
                }
                else
                {
                    int rightLength = (buffer.Length - head);

                    if (rightLength >= count)
                    {
                        copyTo(buffer, head, destination, 0, count);
                    }
                    else
                    {
                        copyTo(buffer, head, destination, 0, rightLength);
                        copyTo(buffer, 0, destination, rightLength, count - rightLength);
                    }
                }

                head = (head + count) % buffer.Length;
                this.length -= count;

                if (this.length == 0)
                {
                    head = 0;
                    tail = 0;
                }
                return count;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Dequeue(Stream stream, int count)
        {
            return Dequeue(stream, count, copyToStream);
        }

        /// <summary>
        /// 
        /// </summary>
        public int Dequeue(byte[] buffer, int offset, int count)
        {
            var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            int result = Dequeue(pin.AddrOfPinnedObject() + offset, count, copyToPointer);

            pin.Free();

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        public Stream AsStream(FileAccess access)
        {
            return new MemoryQueueStream(this, access);
        }
    }
}
