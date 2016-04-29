//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using SQLite;
using NachoCore.Brain;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McAction : McAbstrObjectPerAcc
    {

        public enum ActionState
        {
            Hot,
            Open,
            Deferred,
            Completed
        }

        public string Title { get; set; }
        public string Description { get; set; }
        [Indexed(Name="StateAndSort", Order=1)]
        public ActionState State { get; set; }
        [Indexed(Name="StateAndSort", Order=2)]
        public int UserSortOrder { get; set; }
        [Indexed]
        public int EmailMessageId { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime DeferUntilDate { get; set; }
        public DateTime CompletedDate { get; set; }
        public MessageDeferralType DeferralType { get; set; }

        public bool IsHot {
            get {
                return State == ActionState.Hot;
            }
        }

        public bool IsCompleted {
            get {
                return State == ActionState.Completed;
            }
        }

        public bool IsDeferred {
            get {
                return State == ActionState.Deferred;
            }
        }

        public McAction () : base ()
        {
        }

        public static McAction FromMessage (McEmailMessage message)
        {
            var action = new McAction ();
            action.Title = message.Subject;
            action.AccountId = message.AccountId;
            action.EmailMessageId = message.Id;
            action.State = McAction.ActionState.Hot;
            return action;
        }

        public static List<McAction> ActionsForState (int accountId, ActionState state)
        {
            if (accountId == McAccount.GetUnifiedAccount ().Id) {
                var sql = "SELECT " +
                    "a.* " +
                    "FROM McAction a " +
                    "WHERE likelihood(a.State = ?, 0.3) " +
                    "ORDER BY UserSortOrder";
                return NcModel.Instance.Db.Query<McAction> (sql, state);
            }else{
                var sql = "SELECT " +
                    "a.* " +
                    "FROM McAction a " +
                    "WHERE a.AccountId = ? " +
                    "AND likelihood(a.State = ?, 0.3) " +
                    "ORDER BY UserSortOrder";
                return NcModel.Instance.Db.Query<McAction> (sql, accountId, state);
            }
        }

        public static Dictionary<McAction.ActionState, int> StateCounts (int accountId)
        {
            List<StateCountResult> counts;
            if (accountId == McAccount.GetUnifiedAccount ().Id) {
                var sql = "SELECT State, COUNT(*) as \"Count\" FROM McAction GROUP BY State";
                counts = NcModel.Instance.Db.Query<StateCountResult> (sql);
            } else {
                var sql = "SELECT State, COUNT(*) as \"Count\" FROM McAction WHERE accountId = ? GROUP BY State";
                counts = NcModel.Instance.Db.Query<StateCountResult> (sql, accountId);
            }
            var countsByState = new Dictionary<McAction.ActionState, int> ();
            countsByState.Add (ActionState.Hot, 0);
            countsByState.Add (ActionState.Open, 0);
            countsByState.Add (ActionState.Deferred, 0);
            countsByState.Add (ActionState.Completed, 0);
            foreach (var count in counts) {
                countsByState [count.State] += count.Count;
            }
            return countsByState;
        }

        public static McAction ActionForMessage (McEmailMessage message)
        {
            var messages = NcModel.Instance.Db.Query<McAction> ("SELECT a.* FROM McAction a WHERE EmailMessageId = ?", message.Id);
            if (messages.Count > 0) {
                return messages [0];
            }
            return null;
        }

        public static List<McAction> ActionsToUndefer ()
        {
            var date = DateTime.UtcNow;
            var sql = "SELECT " +
                "a.* " +
                "FROM McAction a " +
                "WHERE likelihood(a.State = ?, 0.3) " +
                "AND likelihood(a.DeferUntilDate < ?, 0.1) " +
                "ORDER BY UserSortOrder";
            return NcModel.Instance.Db.Query<McAction> (sql, ActionState.Deferred, date);
        }

        static int LowestSortOrder (ActionState state)
        {
            var sql = "SELECT IFNULL(MIN(UserSortOrder), 0) FROM McAction WHERE State = ?";
            return NcModel.Instance.Db.ExecuteScalar<int> (sql, state) - 1;
        }

        static int HighestSortOrder (ActionState state)
        {
            var sql = "SELECT IFNULL(MAX(UserSortOrder), 0) FROM McAction WHERE State = ?";
            return NcModel.Instance.Db.ExecuteScalar<int> (sql, state) + 1;
        }

        public static void UndeferActions ()
        {
            var actions = ActionsToUndefer ();
            foreach (var action in actions) {
                action.Undefer ();
            }
            // TODO: status ind?
        }

        public void MoveToFront ()
        {
            UserSortOrder = LowestSortOrder (State);
            Update ();
        }

        public void MoveToBack ()
        {
            UserSortOrder = HighestSortOrder (State);
            Update ();
        }

        public void MoveAfterAction (McAction action)
        {
            var targetOrder = action.UserSortOrder + 1;
            var existing = NcModel.Instance.Db.Query<McAction> ("SELECT * FROM McAction WHERE State = ? AND UserSortOrder = ?", State, targetOrder);
            if (existing.Count > 0) {
                NcModel.Instance.Db.Execute ("UPDATE McAction SET UserSortOrder = UserSortOrder + 1 WHERE State = ? AND UserSortOrder >= ?", State, targetOrder);
            }
            UserSortOrder = targetOrder;
            Update ();
        }

        public void Hot ()
        {
            State = ActionState.Hot;
            MoveToFront ();
        }

        public void Unhot ()
        {
            State = ActionState.Open;
            MoveToFront ();
        }

        public void Defer (MessageDeferralType type)
        {
            State = ActionState.Deferred;
            DeferralType = type;
        }

        public void Undefer ()
        {
            State = ActionState.Hot;
            MoveToFront ();
        }

        public void Complete ()
        {
            if (State != ActionState.Completed) {
                CompletedDate = DateTime.UtcNow;
                State = ActionState.Completed;
                MoveToFront ();
            }
        }

        public void Uncomplete (ActionState toState = ActionState.Open)
        {
            if (State == ActionState.Completed) {
                State = toState;
                MoveToFront ();
            }
        }

        public void UpdateMessageFlag ()
        {
            if (IsDeferred) {
                NcMessageDeferral.DeferMessage (Message, DeferralType, DeferUntilDate);
            } else if (DueDate != default(DateTime)) {
                NcMessageDeferral.SetDueDate (Message, DueDate);
            } else {
                // works for defer or due date clearing
                NcMessageDeferral.UndeferMessage (Message);
                // TODO: set a "task" flag without dates?  Backend seems to want dates
            }
        }

        McEmailMessage _Message;
        public McEmailMessage Message
        {
            get {
                if (_Message == null) {
                    _Message = McEmailMessage.QueryById<McEmailMessage> (EmailMessageId);
                }
                return _Message;
            }
        }

        public void RemoveButKeepMessage ()
        {
            var account = McAccount.QueryById<McAccount> (AccountId);
            Message.UpdateWithOCApply<McEmailMessage> ((McAbstrObject record) => {
                var message = record as McEmailMessage;
                message.IsAction = false;
                return true;
            });
            base.Delete ();
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs() {
                Account = account,
                Status = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged)
            });
        }

        private class StateCountResult {

            public McAction.ActionState State { get; set; }
            public int Count { get; set; }
        }

    }
}

