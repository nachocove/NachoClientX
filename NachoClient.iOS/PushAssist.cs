//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoClient.iOS
{
    public class PushAssist
    {
        private static volatile PushAssist instance;
        private static object syncRoot = new Object ();

        public static PushAssist Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new PushAssist ();
                        }
                    }
                }
                return instance; 
            }
        }

        private PushAssist ()
        {
        }

        public void SetDeviceToken (byte[] deviceToken)
        {

        }

        public void ResetDeviceToken ()
        {
        }

        // Once we get device-token, we can do URL-A and get client-token.
        // Once we have client-token, we are ready to use service.
        // 
    }
}

