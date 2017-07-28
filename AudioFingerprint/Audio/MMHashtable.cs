using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AudioFingerprint
{
    /// <summary>
    /// Specialized class which stores a key of type uint and values of int[]
    /// It has a fixed size hash table which cannot grow! and is relatively small
    /// 
    /// It writes it's data to a memorymapped file in memory!
    /// </summary>
    public unsafe class MMHashtable : IDisposable
    {
        private static string MMHFilename = "MMHashtable" + Guid.NewGuid().ToString();
        private static int unique = 0;

        private bool hashtableIsReadonly;
        private int capacity;
        private MemoryMappedFile mmFile;
        private MemoryMappedViewAccessor mmvAccessor;
        private byte* _bytes;
        private long* hashLookup;
        private long memoryUsed; // location where empty data begins
        private long memoryReserved; // location where empty data begins

        /// <summary>
        /// capacity max number of key values that can be entred (resize not supported!)
        /// 
        /// countValies is the maxnumber of values to be stored (memory is calculated for worst case)
        /// </summary>
        public MMHashtable(int capacity, int countValues)
        {
            hashtableIsReadonly = false;
            this.capacity = capacity;

            // Size calculated
            // size (capacity of hashtable)  = 4 bytes
            // capacity with (file)pointer is the array for the hashtable * 8
            // Every entry = 4(key) + 4(len) + capacity * 4
            //
            // calculation for worst case
            memoryReserved = 4 + (this.capacity * 8) +
                            (countValues * 4) + (this.capacity * 8);

            int id = System.Threading.Interlocked.Increment(ref unique);
            mmFile = MemoryMappedFile.CreateNew(MMHFilename + id.ToString(), memoryReserved, MemoryMappedFileAccess.ReadWrite);
            mmvAccessor = mmFile.CreateViewAccessor();
            mmvAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _bytes);

            hashLookup = (long*)(_bytes + 4);
            
            // Write size of hashtable
            *(int*)(_bytes) = this.capacity;
            memoryUsed = 4 + (this.capacity * 8);
        }

        public MMHashtable(byte[] hashtableData)
        {
            hashtableIsReadonly = true;
            memoryReserved = hashtableData.Length;
            memoryUsed = memoryReserved;

            int id = System.Threading.Interlocked.Increment(ref unique);
            mmFile = MemoryMappedFile.CreateNew("MMHashtable" + id.ToString(), memoryReserved, MemoryMappedFileAccess.ReadWrite);
            mmvAccessor = mmFile.CreateViewAccessor();
            mmvAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _bytes);

            // Copy data to Mapped memory
            IntPtr mmPtr = new IntPtr(_bytes);
            Marshal.Copy(hashtableData, 0, mmPtr, hashtableData.Length);

            hashLookup = (long*)(_bytes + 4);
            this.capacity = *(int*)(_bytes);
        }

        ~MMHashtable()
        {
            Dispose(false);
        }

        /// <summary>
        /// Disposes all used resources and deletes the temporary file.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_bytes != null)
                {
                    mmvAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    _bytes = null;
                }
                if (mmvAccessor != null)
                {
                    mmvAccessor.Dispose();
                    mmvAccessor = null;
                }
                if (mmFile != null)
                {
                    mmFile.Dispose();
                    mmFile = null;
                }
            }
        }

        public long MemoryUsed
        {
            get
            {
                return memoryUsed;
            }
        }

        public long MemoryReserved
        {
            get
            {
                return memoryReserved;
            }
        }

        public void Add(uint key, int[] values)
        {
            if (hashtableIsReadonly)
            {
                throw new ArgumentException("MMHashtable is readonly.");
            }

            int index = GetHashCodePosition(key);
            long keyPosition = hashLookup[index];

            // Is er al wat ingevuld?
            int count = 0;
            while (keyPosition != 0)
            {
                // ja dus ga opzoek naar volgende lege entry (als key al niet bestaat natuurlijk)
                uint hash = *(uint*)(_bytes + keyPosition);
                if (hash == key)
                {
                    throw new ArgumentException("An item with the same key has already been added.");
                }
                index++;
                count++;
                // roll around
                if (index >= capacity)
                {
                    index = 0;
                }
                if (count >= capacity)
                {
                    throw new ArgumentException("Hashtable is full.");
                }
                keyPosition = hashLookup[index];
            } // while

            // we hebben een geldige index
            hashLookup[index] = memoryUsed;
            *(uint*)(_bytes + memoryUsed) = key; // store the key value
            *(int*)(_bytes + memoryUsed + 4) = values.Length; /// write length of array
            IntPtr data = new IntPtr(_bytes + memoryUsed + 8);
            Marshal.Copy(values, 0, data, values.Length);
            memoryUsed = memoryUsed + 8 + (values.Length * sizeof(int));
        }

        public bool TryKeyValue(uint key, out int[] values)
        {
            values = new int[0];
            int index = GetHashCodePosition(key);
            long keyPosition = hashLookup[index];

            // Is er al wat ingevuld?
            int count = 0;
            while (keyPosition != 0)
            {
                // ja, zoekop of key == hash
                uint hash = *(uint*)(_bytes + keyPosition);
                if (hash == key)
                {
                    // gevonden 
                    break;
                }
                index++;
                count++;
                // roll around
                if (index >= capacity)
                {
                    index = 0;
                }
                if (count >= capacity)
                {
                    // key not found
                    return false;
                }
                keyPosition = hashLookup[index];
            } // while

            if (keyPosition != 0)
            {
                int len = *(int*)(_bytes + keyPosition + 4);
                values = new int[len];

                IntPtr data = new IntPtr(_bytes + keyPosition + 8);
                Marshal.Copy(data, values, 0, values.Length);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Byte presentation of this hashtable. Can be used to restore this hashtable.
        /// (But can't be written to after that!)
        /// </summary>
        public byte[] HashtableAsByteArray
        {
            get
            {
                byte[] data = new byte[memoryUsed];
                IntPtr mmPtr = new IntPtr(_bytes);
                Marshal.Copy(mmPtr, data, 0, (int)memoryUsed);

                return data;
            }
        }


        private int GetHashCodePosition(uint key)
        {
            int num = key.GetHashCode() & 0x7fffffff;
            int index = num % (int)capacity;
            return index;
        }

    }
}
