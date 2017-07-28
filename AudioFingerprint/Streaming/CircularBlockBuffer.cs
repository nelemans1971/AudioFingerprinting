#region License
// Copyright (c) 2013 Stichting Centrale Discotheek Rotterdam. All rights reserved.
// 
// website: http://www.muziekweb.nl
// e-mail:  info@cdr.nl
// 
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation; either version 2 of the License, or 
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
// for more details.
// 
// You should have received a copy of the GNU General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA. or 
// visit www.gnu.org.
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioFingerprint.Audio
{
    /// <summary>
    /// YN 2013-08-27
    /// 
    /// CircularBuffer which grows automatically when it's full, in set block sizes. (BLOCKSIZE=64kB).
    /// It uses the same memory when data is dropped so as to minimize memory alloc calls.
    /// This implementation is needed because for audio you don't want to drop data
    /// 
    /// YN 2014-04-08
    /// lock is hier voldoende denk ik (geen timeout)
    /// </summary>
    public class CircularBlockBuffer
    {
        private const int BLOCKSIZE = 64 * 1024; // default 64kB

        private object lockVAR = new object();
        private List<byte[]> blockList = null;

        private long writePtr; // point to write to
        private long readPtr; // Point to read from
        private long usedBytes; // Point to read from
        private long totalSize;

        public CircularBlockBuffer()
        {
            InitVars(BLOCKSIZE*2);
        }

        public CircularBlockBuffer(int initialSizeInBytes)
        {
            InitVars(initialSizeInBytes);
        }

        private void InitVars(int initialSize)
        {
            writePtr = 0;
            readPtr = 0;
            usedBytes = 0;
            totalSize = 0;

            GrowBuffer(initialSize);
        }

        private void GrowBuffer(long newSize)
        {
            if (blockList == null)
            {
                blockList = new List<byte[]>();
            }

            int oldBlockCount = blockList.Count;
            int newBlockCount = Convert.ToInt32(System.Math.Ceiling((double)newSize / BLOCKSIZE));

            // Only grow not smaller!
            if (newBlockCount > oldBlockCount)
            {
                int blocks = newBlockCount - oldBlockCount;
                
                if (ReadAndWriteInSameBlock(writePtr, readPtr) && writePtr < readPtr)
                {
                    // Data moet verhuis worden voordat we kunnen vergroten!
                    // stap 1 maak nieuw buffer aan
                    byte[] buffer = new byte[BLOCKSIZE];
                    // stap 2 kopieer data vanaf readPtr naar nieuwe blok
                    int block = Position2BlockNumber(readPtr);
                    int offset = Position2BlockNumberOffset(readPtr);
                    Buffer.BlockCopy(blockList[block], offset, buffer, offset, Convert.ToInt32(BLOCKSIZE - offset));

                    // voeg op juiste positie toe 
                    if (block + 1 >= blockList.Count)
                    {
                        blockList.Add(buffer);
                    }
                    else
                    {
                        blockList.Insert(block + 1, buffer);
                    }
                    // Nu ReadPtr aanpassen
                    readPtr = BlockNumber2Position(block + 1, offset);

                    blocks--; // verminder met 1 want die hebben we al gedaan
                }


                for (int i = 0; i < blocks; i++)
                {
                    byte[] buffer = new byte[BLOCKSIZE];
                    int writeBlock = Position2BlockNumber(writePtr);

                    // voeg op juiste positie toe 
                    if (writeBlock + 1 >= blockList.Count)
                    {
                        blockList.Add(buffer);
                        // ReadPtr zal bij add nooit hoeven te worden aangepast
                    }
                    else
                    {
                        blockList.Insert(writeBlock + 1, buffer);

                        // moeten we readptr aanpassen?
                        if (readPtr > writePtr)
                        {
                            // Ja
                            readPtr += BLOCKSIZE;
                        }
                    }
                }
            }

            // important to recalculate here (if called from 
            // initVars();
            totalSize = (long)blockList.Count * BLOCKSIZE;
        }

        private int Position2BlockNumber(long p)
        {
            return Convert.ToInt32(System.Math.Floor((double)p / BLOCKSIZE));
        }

        private int Position2BlockNumberOffset(long p)
        {
            return Convert.ToInt32(p % BLOCKSIZE);
        }

        private long BlockNumber2Position(int b, int offset = 0)
        {
            return (Convert.ToInt64(b) * BLOCKSIZE) + Convert.ToInt64(offset);
        }

        private int NextBlockNumber(int b)
        {
            b++;
            if (b >= blockList.Count)
            {
                b = 0;
            } 

            return b;
        }

        private bool ReadAndWriteInSameBlock(long p1, long p2)
        {
            return (Position2BlockNumber(p1) == Position2BlockNumber(p2));
        }
        
        public bool Write(byte[] buffer)
        {
            return Write(buffer, 0, buffer.Length);
        }

        public bool Write(byte[] buffer, int bufferOffset, int count)
        {
            try
            {
                lock (lockVAR)
                {
                    if (count > FreeBytes)
                    {
                        // Need a smart grow options
                        GrowBuffer(totalSize + (count - FreeBytes));
                    }

                    // the data will always fit. Just need to write it to the right blocks
                    int todoCount = count;
                    // Step 1 fill current block
                    int block = Position2BlockNumber(writePtr);
                    int offset = Position2BlockNumberOffset(writePtr);
                    int writeCount = BLOCKSIZE - offset;
                    if (writeCount > todoCount)
                    {
                        writeCount = todoCount;
                    }
                    Buffer.BlockCopy(buffer, bufferOffset, blockList[block], offset, writeCount);
                    todoCount -= writeCount;
                    bufferOffset += writeCount;

                    while (todoCount > 0)
                    {
                        block = NextBlockNumber(block);
                        writeCount = BLOCKSIZE;
                        if (writeCount > todoCount)
                        {
                            writeCount = todoCount;
                        }

                        Buffer.BlockCopy(buffer, bufferOffset, blockList[block], 0, writeCount);
                        todoCount -= writeCount;
                        bufferOffset += writeCount;
                    } // while

                    // Adjust writePtr
                    writePtr += count;
                    if (writePtr >= totalSize)
                    {
                        writePtr = writePtr - totalSize;
                    }
                    usedBytes += count;
                } //lock
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(string.Format("[CDR.LibRTMP.Media.CircularBlockBuffer.Write]: {0}", e.ToString()));
            }

            return true;
        }

        /// <summary>
        /// Writes counts bytes from internal circular buffer to buffer
        /// return number of bytes write to the buffer in case
        /// requested count could not be fulfilled
        /// </summary>
        public int Read(byte[] buffer, int count)
        {
            int doneCount = 0;
            try
            {
                lock (lockVAR)
                {
                    if (count > usedBytes)
                    {
                        count = Convert.ToInt32(usedBytes);
                    }

                    // the data will always fit. Just need to write it to the right blocks
                    int todoCount = count;
                    int bufferOffset = 0;
                    // Step 1 fill current block
                    int block = Position2BlockNumber(readPtr);
                    int offset = Position2BlockNumberOffset(readPtr);
                    int readCount = BLOCKSIZE - offset;
                    if (readCount > todoCount)
                    {
                        readCount = todoCount;
                    }
                    Buffer.BlockCopy(blockList[block], offset, buffer, bufferOffset, readCount);
                    todoCount -= readCount;
                    bufferOffset += readCount;
                    doneCount += readCount;

                    while (todoCount > 0)
                    {
                        block = NextBlockNumber(block);
                        readCount = BLOCKSIZE;
                        if (readCount > todoCount)
                        {
                            readCount = todoCount;
                        }

                        Buffer.BlockCopy(blockList[block], 0, buffer, bufferOffset, readCount);
                        todoCount -= readCount;
                        bufferOffset += readCount;
                        doneCount += readCount;
                    } // while

                    // Adjust readPtr
                    readPtr += count;
                    if (readPtr >= this.totalSize)
                    {
                        readPtr = readPtr - this.totalSize;
                    }
                    // adjust size
                    this.usedBytes -= count;
                } //lock
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(string.Format("[CDR.LibRTMP.Media.CircularBlockBuffer.Read]: {0}", e.ToString()));
            }

            return doneCount;
        }

        /// <summary>
        /// Clear the data, leaves the memory allocation as is
        /// </summary>
        public void Clear()
        {
            lock (lockVAR)
            {
                InitVars(blockList.Count * BLOCKSIZE);
            }
        }

        /// <summary>
        /// Number of bytes used
        /// </summary>
        public long UsedBytes
        {
            get
            {
                lock (lockVAR)
                {
                    return usedBytes;
                }
            }
        }

        /// <summary>
        /// Number of bytes free (without growing the buffer)
        /// </summary>
        public long FreeBytes
        {
            get
            {
                lock (lockVAR)
                {
                    return (totalSize - usedBytes);
                }
            }
        }

        /// <summary>
        /// Total memory allocated
        /// </summary>
        public long TotalMemoryAllocated
        {
            get
            {
                lock (lockVAR)
                {
                    return (blockList.Count * BLOCKSIZE);
                }
            }
        }

        /// <summary>
        /// Does the buffer contain any data?
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                lock (lockVAR)
                {
                    return (usedBytes == 0);
                }
            }
        }
    }
}
