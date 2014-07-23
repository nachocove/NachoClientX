//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Text;
using MonoTouch.Foundation;
using MonoTouch.AddressBook;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoPlatform
{
    public sealed class Contacts : IPlatformContacts
    {
        private static volatile Contacts instance;
        private static object syncRoot = new Object();

        private Contacts ()
        {
        }

        public static Contacts Instance
        {
            get 
            {
                if (instance == null) 
                {
                    lock (syncRoot) 
                    {
                        if (instance == null) 
                            instance = new Contacts ();
                    }
                }
                return instance;
            }
        }

        public IEnumerable<McContact> GetContacts ()
        {
            NSError err; 
            var ab = ABAddressBook.Create (out err);
            if (null != err) {
                Log.Error (Log.LOG_SYS, "ABAddressBook.Create: {0}", GetNSErrorString (err));
                return null;
            }
            if (ABAddressBook.GetAuthorizationStatus () != ABAuthorizationStatus.Authorized) {
                InvokeOnUIThread.Instance.Invoke (() => {
                    ab.RequestAccess ((granted, reqErr) => {
                        if (null != reqErr) {
                            Log.Error (Log.LOG_SYS, "ABAddressBook.RequestAccess: {0}", GetNSErrorString (reqErr));
                            return;
                        }
                        if (granted) {
                            Log.Info (Log.LOG_SYS, "ABAddressBook.RequestAccess authorized.");
                        }
                    });
                });
                return null;
            }
            var sources = ab.GetAllSources ();
            foreach (var source in sources) {
                switch (source.SourceType) {
                case ABSourceType.Exchange:
                    continue;
                default:
                    Log.Info (Log.LOG_SYS, "Processing source {0}", source.SourceType);
                    var peeps = ab.GetPeople (source);
                    // peeps [0].Id;
                    // peeps [0].ModificationDate;
                    // peeps [0].CreationDate;
                    break;
                }
            }
            return null;
        }

        private static string GetNSErrorString (NSError nsError)
        {
            try {
                StringBuilder sb = new StringBuilder ();
                sb.AppendFormat ("Error Code: {0}:", nsError.Code.ToString ());
                sb.AppendFormat ("Description: {0}", nsError.LocalizedDescription);
                var userInfo = nsError.UserInfo;
                for (int i = 0; i < userInfo.Keys.Length; i++) {
                    sb.AppendFormat ("[{0}]: {1}\r\n", userInfo.Keys [i].ToString (), userInfo.Values [i].ToString ());
                }
                return sb.ToString ();
            } catch {
                return "";     
            }
        }
    }
}

