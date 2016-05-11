//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using System.Linq;

namespace NachoCore.Utils
{

    public interface MessagesSyncManagerDelegate {

        void MessagesSyncDidComplete (MessagesSyncManager manager);
        void MessagesSyncDidTimeOut (MessagesSyncManager manager);

    }

    public class MessagesSyncManager
    {
     
        public bool IsSyncing {
            get {
                return SyncTokens != null;
            }
        }
        public MessagesSyncManagerDelegate Delegate;

        bool IsListeningForStatusInd = false;
        List<string> SyncTokens = null;
        int SyncTimeoutSeconds = 30;
        NcTimer SyncTimeoutTimer = null;

        public MessagesSyncManager ()
        {
        }

        public bool SyncEmailMessages (NachoEmailMessages messages)
        {
            if (!IsSyncing) {
                StartListeningForStatusInd ();
                var result = messages.StartSync ();
                if (!result.isError ()) {
                    var tokens = result.Value as string;
                    if (tokens != null) {
                        SyncTokens = new List<string> (tokens.Split (new char[] { ',' }));
                        SyncTimeoutTimer = new NcTimer ("MessagesSyncManager_SyncTimeout", HandleSyncTimeout, null, SyncTimeoutSeconds * 1000, 0);
                        return true;
                    }
                }
            } else {
                // repeat calls while syncing report success because callers should behave the same as if syncing started for the first time
                // but ideally, callers should not make repeat calls
                return true;
            }
            return false;
        }

        public void PauseEvents ()
        {
            StopListeningForStatusInd ();
        }

        public void ResumeEvents ()
        {
            StartListeningForStatusInd ();
            CheckPendingsForSyncComplete ();
        }

        public void Cancel ()
        {
            if (SyncTimeoutTimer != null) {
                SyncTimeoutTimer.Dispose ();
                SyncTimeoutTimer = null;
            }
            SyncTokens = null;
        }

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd) {
                IsListeningForStatusInd = true;
                NcApplication.Instance.StatusIndEvent += StatusIndCallback;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndCallback;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (s.Status.SubKind == NcResult.SubKindEnum.Info_SyncSucceeded || s.Status.SubKind == NcResult.SubKindEnum.Error_SyncFailed) {
                if (SyncTokens != null) {
                    if (s.Tokens != null) {
                        foreach (var token in s.Tokens) {
                            SyncTokens.Remove (token);
                        }
                    }
                    if (SyncTokens.Count == 0) {
                        SyncingComplete ();
                    }
                }
            }
        }

        void HandleSyncTimeout (object state)
        {
            SyncTokens = null;
            SyncTimeoutTimer = null;
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                Delegate.MessagesSyncDidTimeOut (this);
            });
        }

        void CheckPendingsForSyncComplete ()
        {
            if (SyncTokens != null) {
                var tokens = new List<string> (SyncTokens);
                foreach (var token in tokens) {
                    var pendings = McPending.QueryByToken (NcApplication.Instance.Account.Id, token);
                    if (pendings.Count () > 0) {
                        var pending = pendings.First ();
                        if (pending.State == McPending.StateEnum.Failed || pending.State == McPending.StateEnum.Deleted) {
                            SyncTokens.Remove (token);
                        }
                    } else {
                        SyncTokens.Remove (token);
                    }
                }
                if (SyncTokens.Count == 0) {
                    SyncingComplete ();
                }
            }
        }

        void SyncingComplete ()
        {
            if (SyncTimeoutTimer != null) {
                SyncTimeoutTimer.Dispose ();
                SyncTimeoutTimer = null;
            }
            SyncTokens = null;
            StopListeningForStatusInd ();
            Delegate.MessagesSyncDidComplete (this);
        }
    }
}

