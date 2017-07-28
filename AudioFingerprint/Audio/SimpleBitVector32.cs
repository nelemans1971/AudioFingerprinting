using System;

namespace AudioFingerprint
{
    public struct SimpleBitVector32
    {
        private static uint[] bitMasks = new uint[32] 
        { 
            (uint)0x00000001 << 0, (uint)0x00000001 << 1,  (uint)0x00000001 << 2, (uint)0x00000001 << 3,
            (uint)0x00000001 << 4, (uint)0x00000001 << 5, (uint)0x00000001 << 6, (uint)0x00000001 << 7,
            (uint)0x00000001 << 8, (uint)0x00000001 << 9, (uint)0x00000001 << 10, (uint)0x00000001 << 11,
            (uint)0x00000001 << 12, (uint)0x00000001 << 13, (uint)0x00000001 << 14, (uint)0x00000001 << 15,
            (uint)0x00000001 << 16, (uint)0x00000001 << 17, (uint)0x00000001 << 18, (uint)0x00000001 << 19,
            (uint)0x00000001 << 20, (uint)0x00000001 << 21, (uint)0x00000001 << 22, (uint)0x00000001 << 23,
            (uint)0x00000001 << 24, (uint)0x00000001 << 25, (uint)0x00000001 << 26, (uint)0x00000001 << 27,
            (uint)0x00000001 << 28, (uint)0x00000001 << 29, (uint)0x00000001 << 30, (uint)0x00000001 << 31
        };

        private uint data;

        public SimpleBitVector32(uint data)
        {
            this.data = data;
        }

        public SimpleBitVector32(int data)
        {
            this.data = unchecked((uint)data);
        }

        public uint UInt32Value
        {
            get
            {
                return data;
            }
            set
            {
                data = value;
            }
        }

        public int Int32Value
        {
            get
            {
                return unchecked((int)data);
            }
            set
            {
                data = unchecked((uint)value);
            }
        }

        public bool this[byte bitNumber]
        {
            get
            {
                return (data & bitMasks[bitNumber]) != 0;
            }
            set
            {
                uint _data = data;
                if (value)
                {
                    data = _data | bitMasks[bitNumber];
                }
                else
                {
                    data = _data & ~bitMasks[bitNumber];
                }
            }
        }

        public void Set(byte bitNumber)
        {
            data |= bitMasks[bitNumber];
        }

        public void Clear(byte bitNumber)
        {
            data &= ~bitMasks[bitNumber];
        }

        public void Toggle(byte bitNumber)
        {
            data ^= bitMasks[bitNumber];
        } 
    }
}