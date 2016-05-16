////  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
////
//using System;
//using System.Linq;
//using System.Collections.Generic;
//using NachoCore;
//using NachoCore.Utils;
//using NachoCore.Model;
//using NachoPlatform;
//
//namespace NachoCore
//{
//    public class NcContactManager
//    {
//        private static volatile NcContactManager instance;
//        private static object syncRoot = new Object ();
//        bool mustRefreshContacts;
//        private static Object StaticLockObj = new Object ();
//
//        public event EventHandler ContactsChanged;
//
//        INachoContacts list;
//        INachoContacts hotList;
//        INachoContacts recentList;
//
//        public static NcContactManager Instance {
//            get {
//                if (instance == null) {
//                    lock (syncRoot) {
//                        if (instance == null)
//                            instance = new NcContactManager ();
//                    }
//                }
//                return instance; 
//            }
//        }
//
//        protected NcContactManager ()
//        {
//            mustRefreshContacts = true;
//            list = new NachoContacts (new List<NcContactIndex> ());
//            hotList = new NachoContacts (new List<NcContactIndex> ());
//            recentList = new NachoContacts (new List<NcContactIndex> ());
//            // Watch for changes from the back end
//            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
//                var s = (StatusIndEventArgs)e;
//                if (NcResult.SubKindEnum.Info_ContactSetChanged == s.Status.SubKind) {
//                    MaybeLoadContacts ();
//                }
//            };
//        }
//
//        protected void MaybeLoadContacts ()
//        {
//            if (null == ContactsChanged) {
//                mustRefreshContacts = true;
//                return;
//            }
//            if (0 == ContactsChanged.GetInvocationList ().Count ()) {
//                mustRefreshContacts = true;
//                return;
//            }
//            if (false == mustRefreshContacts) {
//                return;
//            }
//            mustRefreshContacts = false;
//            LoadContacts ();
//        }
//
//        protected void LoadContacts ()
//        {
//            // Refresh in background    
//            System.Threading.ThreadPool.QueueUserWorkItem (delegate {
//                lock (StaticLockObj) {
//                    var l = McContact.QueryAllContactItems ();
//                    list = new NachoContacts (l);
//                    var h = McContact.QueryAllHotContactItems ();
//                    hotList = new NachoContacts (h);
//                    // TODO: Recent contact folder
//                    if (null != ContactsChanged) {
//                        InvokeOnUIThread.Instance.Invoke (delegate() {  
//                            ContactsChanged.Invoke (this, null);
//                        });
//                    }
//                }
//            });
//        }
//
//        public INachoContacts GetNachoContacts ()
//        {
//            MaybeLoadContacts ();
//            return list;
//        }
//
//        public INachoContacts GetHotNachoContacts ()
//        {
//            MaybeLoadContacts ();
//            return hotList;
//        }
//
//        public INachoContacts GetRecentNachoContacts ()
//        {
//            MaybeLoadContacts ();
//            return recentList;
//        }
//    }
//}
//
