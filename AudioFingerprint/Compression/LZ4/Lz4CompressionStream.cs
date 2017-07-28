using System;
using System.IO;

namespace Lz4Net
{
    /// <summary>
    /// Stream to compress and write back data
    /// </summary>
    public sealed class Lz4CompressionStream : Stream
    {        
        #region overrides without any interested

        /// <summary>[NotSupported]
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin" /> parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin" /> indicating the reference point used to obtain the new position.</param>
        /// <exception cref="T:System.IO.IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The stream does not support seeking, such as if the stream is constructed from a pipe or console output.
        /// </exception>
        /// <exception cref="T:System.ObjectDisposedException">
        /// Methods were called after the stream was closed.
        /// </exception>
        /// <returns>The new position within the current stream.</returns>
        public override long Seek (long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
        /// <exception cref="T:System.NotSupportedException">The stream does not support
        /// both writing and seeking, such as if the stream is constructed from a pipe or
        /// console output. </exception>
        /// <exception cref="T:System.ObjectDisposedException">Methods were called after
        /// the stream was closed. </exception>
        public override void SetLength (long value)
        {
            m_targetStream.SetLength (value);
        }

        /// <summary>[NotSupported]
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> + <paramref name="count" /> - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <exception cref="T:System.ArgumentException">
        /// The sum of <paramref name="offset" /> and <paramref name="count" /> is larger than the buffer length.
        /// </exception>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="buffer" /> is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="offset" /> or <paramref name="count" /> is negative.
        /// </exception>
        /// <exception cref="T:System.IO.IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The stream does not support reading.
        /// </exception>
        /// <exception cref="T:System.ObjectDisposedException">
        /// Methods were called after the stream was closed.
        /// </exception>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        public override int Read (byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <returns>true if the stream supports reading; otherwise, false.
        /// </returns>
        /// <value></value>
        public override bool CanRead
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <returns>true if the stream supports seeking; otherwise, false.
        /// </returns>
        /// <value></value>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <returns>true if the stream supports writing; otherwise, false.
        /// </returns>
        /// <value></value>
        public override bool CanWrite
        {
            get { return true; }
        }

        /// <summary>[NotSupported]
        /// Gets the length in bytes of the stream.
        /// </summary>
        /// <returns>
        /// A long value representing the length of the stream in bytes.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">
        /// A class derived from Stream does not support seeking.
        /// </exception>
        /// <exception cref="T:System.ObjectDisposedException">
        /// Methods were called after the stream was closed.
        /// </exception>
        /// <value></value>
        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>[NotSupported]
        /// Gets or sets the position within the current stream.
        /// </summary>
        /// <returns>
        /// The current position within the stream.
        /// </returns>
        /// <exception cref="T:System.IO.IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The stream does not support seeking.
        /// </exception>
        /// <exception cref="T:System.ObjectDisposedException">
        /// Methods were called after the stream was closed.
        /// </exception>
        /// <value></value>
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        #endregion

        /// <summary>
        /// The Stream we're writing to
        /// </summary>
        private Stream m_targetStream;

        /// <summary>
        /// Write buffer
        /// </summary>
        private readonly byte[] m_writeBuffer;

        /// <summary>
        /// Current position in write buffer
        /// </summary>
        private int m_writeBufferOffset;

        /// <summary>
        /// Buffer where compressed data is stored
        /// </summary>
        private byte[] m_compressedBuffer = null;

        /// <summary>
        /// If the target stream should be close on Dispose
        /// </summary>
        private readonly bool m_closeStream;

        /// <summary>
        /// The selected compression Move
        /// </summary>
        private Lz4Mode m_compressionMode;

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <exception cref="T:System.IO.IOException">
        /// An I/O error occurs.
        /// </exception>
        public override void Flush ()
        {
            if (m_writeBufferOffset > 0)
            {
                int sz = Lz4.Compress (m_writeBuffer, 0, m_writeBufferOffset, ref m_compressedBuffer, m_compressionMode);
                m_targetStream.Write (m_compressedBuffer, 0, sz);
                m_writeBufferOffset = 0;         
            }
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count" /> bytes from <paramref name="buffer" /> to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="T:System.ArgumentException">
        /// The sum of <paramref name="offset" /> and <paramref name="count" /> is greater than the buffer length.
        /// </exception>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="buffer" /> is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// <paramref name="offset" /> or <paramref name="count" /> is negative.
        /// </exception>
        /// <exception cref="T:System.IO.IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The stream does not support writing.
        /// </exception>
        /// <exception cref="T:System.ObjectDisposedException">
        /// Methods were called after the stream was closed.
        /// </exception>
        public override void Write (byte[] buffer, int offset, int count)
        {
            // we have 3 options here:
            // buffer can still be filled --> we fill
            // buffer is full --> we flush
            // buffer is overflood --> we flush and refill

            // 1. there is enough room, the buffer is not full
            int lengthToCauseFlush = m_writeBuffer.Length - m_writeBufferOffset;
            if (count <= lengthToCauseFlush)
            {
                Buffer.BlockCopy(buffer, offset, m_writeBuffer, m_writeBufferOffset, count);
                m_writeBufferOffset += count;
                // 2. same size: write
                if (lengthToCauseFlush == 0)
                    Flush();
            }
            // 3. buffer overflow: we split
            else
            {
                // this first Write will cause a flush
                Write(buffer, offset, lengthToCauseFlush);
                // this one will refill
                Write(buffer, offset + lengthToCauseFlush, count - lengthToCauseFlush);
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.IO.Stream" /> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                Flush();
            }
            base.Dispose(disposing);
            if (m_closeStream && m_targetStream != null)
            {
                m_targetStream.Dispose();                
            }
            m_targetStream = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibCompressionStream" /> class.
        /// </summary>
        /// <param name="targetStream">The target stream.</param>
        /// <param name="writeBuffer">The write buffer.</param>
        /// <param name="compressionBuffer">The compression buffer.</param>
        /// <param name="closeStream">The close stream.</param>
        public Lz4CompressionStream (Stream targetStream, byte[] writeBuffer, byte[] compressionBuffer, Lz4Mode mode = Lz4Mode.Fast, bool closeStream = false)
        {
            m_closeStream = closeStream;
            m_targetStream = targetStream;
            m_writeBuffer = writeBuffer;
            m_compressedBuffer = compressionBuffer;
            m_compressionMode = mode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibCompressionStream" /> class.
        /// </summary>
        /// <param name="targetStream">The target.</param>
        /// <param name="bufferSize">Size of the buffer.</param>
        /// <param name="closeStream">The close stream.</param>
        public Lz4CompressionStream (Stream targetStream, int bufferSize, Lz4Mode mode = Lz4Mode.Fast, bool closeStream = false)
            : this (targetStream, new byte[bufferSize], new byte[Lz4.LZ4_compressBound (bufferSize)], mode, closeStream)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibCompressionStream" /> class.
        /// </summary>
        /// <param name="targetStream">The target.</param>
        /// <param name="closeStream">The close stream.</param>
        public Lz4CompressionStream (Stream targetStream, Lz4Mode mode = Lz4Mode.Fast, bool closeStream = false)
            : this (targetStream, 1 << 18, mode, closeStream) // 256 kb
        {
        }
    }
}
