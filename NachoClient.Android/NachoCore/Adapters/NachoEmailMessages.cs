//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public delegate void NachoMessagesRefreshCompletionDelegate (bool changed, List<int> adds, List<int> deletes);

    public class NachoEmailMessages
    {

        public virtual int Count ()
        {
            return 0;
        }

        public virtual bool Refresh (out List<int> adds, out List<int> deletes)
        {
            adds = null;
            deletes = null;
            return false;
        }

        public virtual bool HasBackgroundRefresh ()
        {
            return false;
        }

        public virtual void BackgroundRefresh (NachoMessagesRefreshCompletionDelegate completionAction)
        {
            if (null != completionAction) {
                completionAction (false, null, null);
            }
        }

        public virtual McEmailMessageThread GetEmailThread (int i)
        {
            NcAssert.CaseError ();
            return null;
        }

        public virtual List<McEmailMessageThread> GetEmailThreadMessages (int i)
        {
            NcAssert.CaseError ();
            return null;
        }

        public virtual string DisplayName ()
        {
            return "";
        }

        public virtual NcResult StartSync ()
        {
            return NachoSyncResult.DoesNotSync ();
        }

        public virtual void RefetchSyncTime ()
        {
        }

        public virtual DateTime? LastSuccessfulSyncTime ()
        {
            return null;
        }

        public virtual NachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            return null;
        }

        public virtual bool HasFilterSemantics ()
        {
            return false;
        }

        public virtual FolderFilterOptions FilterSetting {
            get {
                return FolderFilterOptions.All;
            }
            set {
                NcAssert.CaseError (string.Format ("Attempting to set filter setting to {0} when the view doesn't support filtering.", value.ToString ()));
            }
        }

        public virtual FolderFilterOptions[] PossibleFilterSettings {
            get {
                return new FolderFilterOptions[] { FolderFilterOptions.All };
            }
        }

        public virtual FolderFilterOptions PossibleFilterSettingsMask {
            get {
                FolderFilterOptions mask = 0;
                foreach (var option in PossibleFilterSettings) {
                    mask |= option;
                }
                return mask;
            }
        }

        public virtual bool HasOutboxSemantics ()
        {
            return false;
        }

        public virtual bool HasDraftsSemantics ()
        {
            return false;
        }

        public virtual bool HasSentSemantics ()
        {
            return false;
        }

        public virtual bool IsCompatibleWithAccount (McAccount account)
        {
            return false;
        }

        public virtual bool IncludesMultipleAccounts ()
        {
            return false;
        }

        #region Message Caching

        int[] first = new int[3] { -1, -1, -1 };
        List<McEmailMessage>[] cache = new List<McEmailMessage>[3];
        const int CACHEBLOCKSIZE = 32;

        public void ClearCache ()
        {
            for (var i = 0; i < first.Length; i++) {
                first [i] = -1;
            }
        }

        public McEmailMessage GetCachedMessage (int i)
        {
            var block = i / CACHEBLOCKSIZE;
            var cacheIndex = block % 3;

            if (block != first [cacheIndex]) {
                MaybeReadBlock (block);
            } else {
                MaybeReadBlock (block - 1);
                MaybeReadBlock (block + 1);
            }

            var index = i % CACHEBLOCKSIZE;
            return cache [cacheIndex] [index];
        }

        void MaybeReadBlock (int block)
        {
            if (0 > block) {
                return;
            }
            var cacheIndex = block % 3;
            if (block == first [cacheIndex]) {
                return;
            }
            var start = block * CACHEBLOCKSIZE;
            var finish = (Count () < (start + CACHEBLOCKSIZE)) ? Count () : start + CACHEBLOCKSIZE;
            var indexList = new List<int> ();
            for (var i = start; i < finish; i++) {
                var thread = GetEmailThread (i);
                if (thread == null) {
                    indexList.Add (0);
                } else {
                    indexList.Add (thread.FirstMessageSpecialCaseIndex ());
                }
            }
            cache [cacheIndex] = new List<McEmailMessage> ();
            var resultList = McEmailMessage.QueryForSet (indexList);
            // Reorder the list, add in nulls for missing entries
            foreach (var i in indexList) {
                var result = resultList.Find (x => x.Id == i);
                cache [cacheIndex].Add (result);
            }
            first [cacheIndex] = block;
            UpdateCachedPropertiesForBlock (cache [cacheIndex]);
        }

        void UpdateCachedPropertiesForBlock (List<McEmailMessage> messages)
        {
            // Get portraits
            var fromAddressIdList = new List<int> ();
            foreach (var message in messages) {
                if (null != message) {
                    if ((0 != message.FromEmailAddressId) && !fromAddressIdList.Contains (message.FromEmailAddressId)) {
                        fromAddressIdList.Add (message.FromEmailAddressId);
                    }
                }
            }
            // Assign matching portrait ids to email messages
            var portraitIndexList = McContact.QueryForPortraits (fromAddressIdList);
            foreach (var portraitIndex in portraitIndexList) {
                foreach (var message in messages) {
                    if (null != message) {
                        if (portraitIndex.EmailAddress == message.FromEmailAddressId) {
                            message.cachedPortraitId = portraitIndex.PortraitId;
                        }
                    }
                }
            }
        }

        public bool MaybeUpdateMessageInCache (int id)
        {
            foreach (var c in cache) {
                if (null == c) {
                    continue;
                }
                for (int i = 0; i < c.Count; i++) {
                    var m = c [i];
                    if (null != m) {
                        if (m.Id == id) {
                            c [i] = McEmailMessage.QueryById<McEmailMessage> (id);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        #endregion

        #region Ignored messages

        HashSet<int> ignoredMessageIds = null;
        DateTime lastIgnoredUpdateTime = default(DateTime);
        object ignoredMessagesLock = new object ();

        public virtual void IgnoreMessage (int messageId)
        {
            lock (ignoredMessagesLock) {
                if (null == ignoredMessageIds) {
                    ignoredMessageIds = new HashSet<int> ();
                }
                ignoredMessageIds.Add (messageId);
                lastIgnoredUpdateTime = DateTime.UtcNow;
            }
        }

        protected void RemoveIgnoredMessages (List<McEmailMessageThread> threadList)
        {
            HashSet<int> copy;
            lock (ignoredMessagesLock) {
                if (null == ignoredMessageIds) {
                    return;
                }
                if (DateTime.UtcNow - lastIgnoredUpdateTime > TimeSpan.FromSeconds (60)) {
                    ignoredMessageIds = null;
                    return;
                }
                // Make a copy of the ignored message IDs so the lock doesn't have to be held
                // while iterating through the entire thread list.
                copy = new HashSet<int> (ignoredMessageIds);
            }
            for (int i = threadList.Count - 1; i >= 0; --i) {
                if (copy.Contains (threadList [i].FirstMessageId)) {
                    threadList.RemoveAt (i);
                }
            }
        }

        #endregion
    }

    public static class NachoSyncResult
    {
        public static NcResult DoesNotSync ()
        {
            return NcResult.Error (NcResult.SubKindEnum.Error_ClientOwned);
        }

        public static bool DoesNotSync (NcResult nr)
        {
            return nr.isError () && (NcResult.SubKindEnum.Error_ClientOwned == nr.SubKind);
        }
    }
}
