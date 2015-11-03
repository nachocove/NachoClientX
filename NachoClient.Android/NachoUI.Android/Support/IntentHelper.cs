//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

namespace NachoClient.AndroidClient
{
    public class IntentHelper
    {
        private static Dictionary<string, object> data = new Dictionary<string, object> ();

        public static string StoreValue<T> (T value) where T : class
        {
            string key = Guid.NewGuid ().ToString ();
            data [key] = value;
            return key;
        }

        public static T RetrieveValue<T> (string key) where T : class
        {
            object value;
            if (data.TryGetValue(key, out value)) {
                data.Remove (key);
            }
            return value as T;
        }
    }
}

