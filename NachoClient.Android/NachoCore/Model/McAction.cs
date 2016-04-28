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
            MoveToBack ();
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

        public void Uncomplete ()
        {
            if (State == ActionState.Completed) {
                State = ActionState.Open;
                MoveToFront ();
            }
        }

        public void UpdateMessageFlag ()
        {
            var thread = new McEmailMessageThread ();
            thread.FirstMessageId = EmailMessageId;
            thread.MessageCount = 1;
            if (IsDeferred) {
                NcMessageDeferral.DeferThread (thread, DeferralType, DeferUntilDate);
            } else if (DueDate != default(DateTime)) {
                NcMessageDeferral.SetDueDate (thread, DueDate);
            } else {
                // works for defer or due date clearing
                NcMessageDeferral.UndeferThread (thread);
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

    }
}

