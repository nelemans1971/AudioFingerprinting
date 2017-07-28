using System;
using System.IO;

namespace Lz4Net
{
    /// <summary>
    /// Stream to compress and write back data
    /// </summary>
    public sealed class Lz4DecompressionStream : Stream
    {
         #region overrides without any interested

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <exception cref="T:System.IO.IOException">
        /// An I/O error occurs.
        /// </exception>
        public override void Flush()
        {
        }

        /// <summary>[NotSupported]
        /// Sets the position within the current stream.
        /// </summary>
        /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
        /// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
        /// <returns>
        /// The new position within the current stream.
        /// </returns>
        /// <exception cref="T:System.IO.IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The stream does not support seeking, such as if the stream is constructed from a pipe or console output.
        /// </exception>
        /// <exception cref="T:System.ObjectDisposedException">
        /// Methods were called after the stream was closed.
        /// </exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        /// <exception cref="T:System.IO.IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output.
        /// </exception>
        /// <exception cref="T:System.ObjectDisposedException">
        /// Methods were called after the stream was closed.
        /// </exception>
        public override void SetLength(long value)
        {
            m_targetStream.SetLength (value);
        }

        /// <summary>[NotSupported]
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count"/> bytes from <paramref name="buffer"/> to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="T:System.ArgumentException">
        /// The sum of <paramref name="offset"/> and <paramref name="count"/> is greater than the buffer length.
        /// </exception>
        /// <exception cref="T:System.ArgumentNullException">
        /// 	<paramref name="buffer"/> is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// 	<paramref name="offset"/> or <paramref name="count"/> is negative.
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
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <value></value>
        /// <returns>true if the stream supports reading; otherwise, false.
        /// </returns>
        public override bool CanRead
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <value></value>
        /// <returns>true if the stream supports seeking; otherwise, false.
        /// </returns>
        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <value></value>
        /// <returns>true if the stream supports writing; otherwise, false.
        /// </returns>
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>[NotSupported]
        /// Gets the length in bytes of the stream.
        /// </summary>
        /// <value></value>
        /// <returns>
        /// A long value representing the length of the stream in bytes.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">
        /// A class derived from Stream does not support seeking.
        /// </exception>
        /// <exception cref="T:System.ObjectDisposedException">
        /// Methods were called after the stream was closed.
        /// </exception>
        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>[NotSupported]
        /// Gets or sets the position within the current stream.
        /// </summary>
        /// <value></value>
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
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        #endregion

        /// <summary>
        /// Stream we're reading from
        /// </summary>
        private Stream m_targetStream;

        /// <summary>
        /// Temporary buffer where raw data is stored.
        /// Kept to be reused from one buffer fill to another
        /// </summary>
        private byte[] m_readBuffer;

        /// <summary>
        /// Unpacked buffer
        /// </summary>
        private byte[] m_unpackedBuffer = null;
        /// <summary>
        /// Read position un unpacked buffer
        /// </summary>
        private int m_unpackedOffset;

        /// <summary>
        /// Length for unpacked data
        /// </summary>
        private int m_unpackedLength;

        /// <summary>
        /// Room for header
        /// </summary>
        const int HeaderSize = 8;
        private readonly byte[] m_header = new byte[HeaderSize];
        private readonly bool m_closeStream;

        /// <summary>
        /// Fills or refills the read buffer.
        /// </summary>
        private void Fill()
        {
            int headerLength = m_targetStream.Read (m_header, 0, HeaderSize);
            // the normal end is here
            if (headerLength == 0)
            {
                m_unpackedBuffer = null;
                return;
            }
            if (headerLength != HeaderSize)
                throw new InvalidDataException("input buffer corrupted (header)");
            int sizeCompressed = Lz4.GetCompressedSize (m_header);
            if (m_readBuffer == null || m_readBuffer.Length < (sizeCompressed + HeaderSize))
            {
                m_readBuffer = new byte[sizeCompressed + HeaderSize];
            }
            Buffer.BlockCopy (m_header, 0, m_readBuffer, 0, HeaderSize);
            int bodyLength = m_targetStream.Read (m_readBuffer, HeaderSize, sizeCompressed);
            if (bodyLength != sizeCompressed)
                throw new InvalidDataException ("input buffer corrupted (body)");
            // decompress
            m_unpackedLength = Lz4.Decompress (m_readBuffer, 0, ref m_unpackedBuffer);
            m_unpackedOffset = 0;
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset"/> and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        /// <exception cref="T:System.ArgumentException">
        /// The sum of <paramref name="offset"/> and <paramref name="count"/> is larger than the buffer length.
        /// </exception>
        /// <exception cref="T:System.ArgumentNullException">
        /// 	<paramref name="buffer"/> is null.
        /// </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// 	<paramref name="offset"/> or <paramref name="count"/> is negative.
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
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (m_unpackedBuffer == null || m_unpackedOffset == m_unpackedLength)
                Fill();

            // to do: something smarter than the double test
            if (m_unpackedBuffer == null)
                return 0;

            // 1. If we don't have enough data available, then split
            if (m_unpackedOffset + count > m_unpackedLength)
            {
                int available = m_unpackedLength - m_unpackedOffset;
                // this is the part we're sure to get
                int r1 = Read(buffer, offset, available);
                // this is the part we're not
                int r2 = Read(buffer, offset + available, count - available);
                return r1 + r2;
            }
            // 2. we have enough buffer, use it
            Buffer.BlockCopy(m_unpackedBuffer, m_unpackedOffset, buffer, offset, count);
            m_unpackedOffset += count;
            return count;
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.IO.Stream"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                Flush ();
            }
            base.Dispose (disposing);
            if (m_closeStream && m_targetStream != null)
            {
                m_targetStream.Dispose ();
            }
            m_targetStream = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibDecompressionStream" /> class.
        /// </summary>
        /// <param name="sourceStream">The source stream.</param>
        /// <param name="compression">The compression.</param>
        /// <param name="closeStream">The close stream.</param>
        public Lz4DecompressionStream (Stream sourceStream, bool closeStream = false)
        {
            m_closeStream = closeStream;
            m_targetStream = sourceStream;
            Fill();
        }
    }
}
