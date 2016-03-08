//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public delegate void NachoMessagesRefreshCompletionDelegate (bool changed, List<int> adds, List<int> deletes);

    public interface INachoEmailMessages
    {
        int Count ();

        /// <summary>
        /// Refresh the email message list. Return true if there is changes; false otherrwise.
        /// </summary>
        bool Refresh (out List<int> adds, out List<int> deletes);

        bool HasBackgroundRefresh ();

        void BackgroundRefresh (NachoMessagesRefreshCompletionDelegate completionAction);

        // Returns the thread, not the messages
        McEmailMessageThread GetEmailThread (int i);

        // Need to use the same query to fetch the thread
        List<McEmailMessageThread> GetEmailThreadMessages (int i);

        string DisplayName ();

        NcResult StartSync ();

        INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread);

        /// <summary>
        /// Can these messages be filtered?
        /// </summary>
        bool HasFilterSemantics ();

        FolderFilterOptions FilterSetting { get; set; }

        FolderFilterOptions[] PossibleFilterSettings { get; }

        bool HasOutboxSemantics ();

        bool HasDraftsSemantics ();

        bool IsCompatibleWithAccount (McAccount account);
    }

    public class NachoEmailMessagesBase : INachoEmailMessages
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

        public virtual INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
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

        public virtual bool HasOutboxSemantics ()
        {
            return false;
        }

        public virtual bool HasDraftsSemantics ()
        {
            return false;
        }

        public virtual bool IsCompatibleWithAccount (McAccount account)
        {
            return false;
        }
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
