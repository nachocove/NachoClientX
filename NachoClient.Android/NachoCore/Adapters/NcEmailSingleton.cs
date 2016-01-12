//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class NcEmailSingleton
    {
        public NcEmailSingleton ()
        {
        }

        static object lockObj = new object ();

        static INachoEmailMessages GetSingleton (int accountId, ConcurrentDictionary<int, INachoEmailMessages> list, Func<INachoEmailMessages> creator)
        {
            INachoEmailMessages messages;

            if (!list.TryGetValue (accountId, out messages)) {
                lock (lockObj) {
                    if (!list.TryGetValue (accountId, out messages)) {
                        messages = creator ();
                        list.TryAdd (accountId, messages);
                    }
                }
            }
            return messages;
        }

        static ConcurrentDictionary<int, INachoEmailMessages> inboxDictionary = new ConcurrentDictionary<int, INachoEmailMessages> ();

        static public INachoEmailMessages InboxSingleton (int accountId)
        {
            return GetSingleton (accountId, inboxDictionary, () => {
                return NcEmailManager.Inbox (accountId);
            });
        }

        static ConcurrentDictionary<int, INachoEmailMessages> priorityDictionary = new ConcurrentDictionary<int, INachoEmailMessages> ();

        static public INachoEmailMessages PrioritySingleton (int accountId)
        {
            return GetSingleton (accountId, priorityDictionary, () => {
                return NcEmailManager.PriorityInbox (accountId);
            });
        }

        static ConcurrentDictionary<INachoEmailMessages, DateTime> lastRefreshDictionary = new ConcurrentDictionary<INachoEmailMessages, DateTime> ();

        static public bool RefreshIfNeeded (INachoEmailMessages messages, out List<int> adds, out List<int> deletes)
        {
            DateTime lastRefresh;
            if (lastRefreshDictionary.TryGetValue (messages, out lastRefresh)) {
                if (lastRefresh > RefreshSpy.SharedInstance.lastChangeStatusIndTime) {
                    adds = null;
                    deletes = null;
                    return false;
                }
            }
            lastRefreshDictionary.AddOrUpdate (messages, (m) => {
                return DateTime.UtcNow;
            }, (m, n) => {
                return DateTime.UtcNow;
            });
            return messages.Refresh (out adds, out deletes);
        }

        class RefreshSpy
        {
            public DateTime lastChangeStatusIndTime = DateTime.MaxValue;

            static RefreshSpy _instance;
            static object lockObject = new object();

            public static RefreshSpy SharedInstance {
                get {
                    if (null == _instance) {
                        lock (lockObject) {
                            if (null == _instance) {
                                _instance = new RefreshSpy ();
                            }
                        }
                    }
                    return _instance;
                }
            }

            private RefreshSpy ()
            {
                lastChangeStatusIndTime = DateTime.UtcNow;
                NcApplication.Instance.StatusIndEvent += NcApplication_Instance_StatusIndEvent;
            }

            void NcApplication_Instance_StatusIndEvent (object sender, EventArgs e)
            {
                var s = (StatusIndEventArgs)e;

                if (null == s.Account || null == NcApplication.Instance.Account || !NcApplication.Instance.Account.ContainsAccount(s.Account.Id)) {
                    return;
                }

                switch (s.Status.SubKind) {
                case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
                case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
                    lastChangeStatusIndTime = DateTime.UtcNow;
                    break;

                }
            }
        }
    }
}

