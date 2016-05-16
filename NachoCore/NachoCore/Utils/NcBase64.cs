//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public class NcBase64
    {
        private enum States {Wait0, Wait1, Wait2, Wait3};
        private States State;
        private byte Byte0;
        private byte Byte1;
        private byte Byte2;

        public int Next (byte encoded)
        {
            uint index;
            if (encoded >= 65 && encoded <= 90) {
                // A-Z
                index = (uint)(encoded - 65);
            } else if (encoded >= 97 && encoded <= 122) {
                // a-z
                index = (uint)(encoded - 97 + 26);
            } else if (encoded >= 48 && encoded <= 57) {
                // 0-9
                index = (uint)(encoded - 48 + 52);
            } else if (encoded == 43) {
                // +
                index = 62;
            } else if (encoded == 47) {
                // /
                index = 63;
            } else {
                // ignore everything else, = included
                return -1;
            }
            switch (State) {
            case States.Wait0:
                Byte0 = (byte)(index << 2);
                State = States.Wait1;
                return -1;
            case States.Wait1:
                Byte0 |= (byte)(index >> 4);
                Byte1 = (byte)(index << 4);
                State = States.Wait2;
                return Byte0;
            case States.Wait2:
                Byte1 |= (byte)(index >> 2);
                Byte2 = (byte)(index << 6);
                State = States.Wait3;
                return Byte1;
            case States.Wait3:
                Byte2 |= (byte)index;
                State = States.Wait0;
                return Byte2;
            default:
                // Need default because of compiler.
                return -1;
            }
        }
    }
}

