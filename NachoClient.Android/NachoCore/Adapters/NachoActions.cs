//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore
{

    public class NachoActions
    {

        protected int AccountId;
        protected McAction.ActionState State;
        List<McAction> Actions;
        List<McAction> UpdatedActions;
        int _CountIgnoringLimit;
        int UpdatedCountIgnoringLimit;
        public int ActionLimit = 0;

        public delegate void RefreshHandler (bool changed, List<int> adds, List<int> deletes);

        public NachoActions (int accountId, McAction.ActionState state)
        {
            AccountId = accountId;
            State = state;
            Actions = new List<McAction> ();
            _CountIgnoringLimit = 0;
        }

        public virtual bool BeginRefresh (out List<int> adds, out List<int> deletes)
        {
            UpdatedActions = McAction.ActionsForState (AccountId, State);
            UpdatedCountIgnoringLimit = UpdatedActions.Count;
            if (ActionLimit > 0 && UpdatedActions.Count > ActionLimit) {
                UpdatedActions = UpdatedActions.GetRange (0, ActionLimit);
            }
            bool changed = AreDifferent(Actions, UpdatedActions, out adds, out deletes);
            return changed;
        }

        public virtual void CommitRefresh ()
        {
            Actions = UpdatedActions;
            UpdatedActions = null;
            _CountIgnoringLimit = UpdatedCountIgnoringLimit;
            UpdatedCountIgnoringLimit = 0;
        }

        public void BackgroundRefresh (RefreshHandler completionHandler)
        {
            NcTask.Run (() => {
                List<int> adds;
                List<int> deletes;
                var changed = BeginRefresh (out adds, out deletes);
                NachoPlatform.InvokeOnUIThread.Instance.Invoke(() => {
                    CommitRefresh ();
                    completionHandler (changed, adds, deletes);
                });
            }, "NachoActions_BackgroundRefresh");
        }

        public int Count ()
        {
            return Actions.Count;
        }

        public int CountIgnoringLimit ()
        {
            return _CountIgnoringLimit;
        }

        public McAction ActionAt (int index)
        {
            return Actions [index];
        }

        public void Move (int index, int toIndex)
        {
            var action = Actions [index];
            Actions.RemoveAt (index);
            Actions.Insert (toIndex, action);
        }

        public bool IncludesMultipleAccounts ()
        {
            return AccountId == McAccount.GetUnifiedAccount ().Id;
        }

        bool AreDifferent (List<McAction> oldItems, List<McAction> newItems, out List<int> adds, out List<int> deletes)
        {
            adds = new List<int> ();
            deletes = new List<int> ();
            if (oldItems == null || oldItems.Count == 0) {
                if (newItems != null) {
                    for (int i = 0; i < newItems.Count; ++i) {
                        adds.Add (i);
                    }
                }
            } else if (newItems == null || newItems.Count == 0) {
                if (oldItems != null) {
                    for (int i = 0; i < oldItems.Count; ++i) {
                        deletes.Add (i);
                    }
                }
            } else {
                var oldIndexesByActionId = new Dictionary<int, int> (oldItems.Count);
                int oldIndex = 0;
                int messageId;
                foreach (var action in oldItems) {
                    oldIndexesByActionId [action.Id] = oldIndex;
                    ++oldIndex;
                }
                int newIndex = 0;
                foreach (var action in newItems) {
                    if (!oldIndexesByActionId.ContainsKey (action.Id)) {
                        adds.Add (newIndex);
                    } else {
                        oldIndexesByActionId.Remove (action.Id);
                    }
                    ++newIndex;
                }
                foreach (var entry in oldIndexesByActionId) {
                    deletes.Add (entry.Value);
                }
            }
            return adds.Count > 0 || deletes.Count > 0;
        }
    }

    public class NachoHotActions : NachoActions
    {

        public int NormalCount { get; private set; }
        public int DeferredCount { get; private set; }
        public int CompletedCount { get; private set; }

        public int UpdatedNormalCount { get; private set; }
        public int UpdatedDeferredCount { get; private set; }
        public int UpdatedCompletedCount { get; private set; }

        public NachoHotActions (int accountId) : base (accountId, McAction.ActionState.Hot)
        {
        }

        public override bool BeginRefresh (out List<int> adds, out List<int> deletes)
        {
            var changed = base.BeginRefresh (out adds, out deletes);
            var counts = McAction.StateCounts (AccountId);
            if (counts [McAction.ActionState.Open] != NormalCount) {
                changed = true;
                UpdatedNormalCount = counts [McAction.ActionState.Open];
            }
            if (counts [McAction.ActionState.Deferred] != DeferredCount) {
                changed = true;
                UpdatedDeferredCount = counts [McAction.ActionState.Deferred];
            }
            if (counts [McAction.ActionState.Completed] != CompletedCount) {
                changed = true;
                UpdatedCompletedCount = counts [McAction.ActionState.Completed];
            }
            return changed;
        }

        public override void CommitRefresh ()
        {
            base.CommitRefresh ();
            NormalCount = UpdatedNormalCount;
            DeferredCount = UpdatedNormalCount;
            CompletedCount = UpdatedCompletedCount;
        }

        public int NonHotCount {
            get {
                return NormalCount + DeferredCount + CompletedCount;
            }
        }

    }
}

