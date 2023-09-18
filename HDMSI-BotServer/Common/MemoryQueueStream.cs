using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiServerIntegrateBot.Common
{
    public class MemoryQueueStream : Stream
    {
        public readonly MemoryQueue Queue;
        public readonly FileAccess Access;
        public override bool CanRead => (Access & FileAccess.Read) != 0;
        public override bool CanSeek => false;
        public override bool CanWrite => (Access & FileAccess.Write) != 0;
        public override long Length => Queue.Length;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public MemoryQueueStream(MemoryQueue queue, FileAccess access)
        {
            this.Queue = queue;
            this.Access = access;
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (CanRead)
            {
                return Queue.Dequeue(buffer, offset, count);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (CanWrite)
            {
                Queue.Enqueue(buffer, offset, count);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
