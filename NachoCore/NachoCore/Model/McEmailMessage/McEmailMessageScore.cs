//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using SQLite;
using NachoCore.Brain;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McEmailMessageScore : McAbstrObjectPerAcc, IScoreStates
    {
        // Id of the corresponding McEmailMessage
        [Indexed]
        public int ParentId { get; set; }

        /// How many times the email is read
        public int TimesRead { get; set; }

        /// How long the user read the email
        public int SecondsRead { get; set; }

        public bool IsRead { get; set; }

        public bool IsReplied { get; set; }

        // First time that the email is notified.
        public DateTime NotificationTime { get; set; }

        // Confidence of the notification time
        public double NotificationVariance { get; set; }

        // First time that the email is read
        public DateTime ReadTime { get; set; }

        // Confidence of the read time
        public double ReadVariance { get; set; }

        // First time that the email is replied
        public DateTime ReplyTime { get; set; }

        // Confidence of the reply time
        public double ReplyVariance { get; set; }

        public static McEmailMessageScore QueryByParentId (int parentId)
        {
            return NcModel.Instance.Db.Query<McEmailMessageScore> (
                "SELECT e.* FROM McEmailMessageScore AS e WHERE likelihood(e.ParentId = ?, 0.1)",
                parentId).SingleOrDefault ();
        }

        public static void DeleteByParentId (int parentId)
        {
            NcAssert.True (NcModel.Instance.IsInTransaction ());
            NcModel.Instance.Db.Execute ("DELETE FROM McEmailMessageScore WHERE ParentId = ?", parentId);
        }

        public static bool ShouldUpdateMinimum (DateTime currentTime, DateTime newTime)
        {
            if (DateTime.MinValue == currentTime) {
                return DateTime.MinValue != newTime;
            }
            if (DateTime.MinValue == newTime) {
                return true; // read status is being cleared
            }
            return (currentTime > newTime);
        }

        public bool UpdateNotificationTime (DateTime notificationTime, double variance)
        {
            var original = NotificationTime;
            var newStates = UpdateWithOCApply<McEmailMessageScore> ((item) => {
                if (!ShouldUpdateMinimum (NotificationTime, notificationTime)) {
                    notificationTime = NotificationTime;
                    variance = NotificationVariance;
                }
                var ems = (McEmailMessageScore)item;
                ems.NotificationTime = notificationTime;
                ems.NotificationVariance = variance;
                return true;
            });
            return original != newStates.NotificationTime;
        }

        public bool UpdateReadTime (DateTime readTime, double variance)
        {
            var original = ReadTime;
            var newStates = UpdateWithOCApply<McEmailMessageScore> ((item) => {
                if (!ShouldUpdateMinimum (ReadTime, readTime)) {
                    readTime = ReadTime;
                    variance = ReadVariance;
                }
                var ems = (McEmailMessageScore)item;
                ems.ReadTime = readTime;
                ems.ReadVariance = variance;
                return true;
            });
            return original != newStates.ReadTime;
        }

        public bool UpdateReplyTime (DateTime replyTime, double variance)
        {
            var original = ReplyTime;
            var newStates = UpdateWithOCApply<McEmailMessageScore> ((item) => {
                if (!ShouldUpdateMinimum (ReplyTime, replyTime)) {
                    replyTime = ReplyTime;
                    variance = ReplyVariance;
                }
                var ems = (McEmailMessageScore)item;
                ems.ReplyTime = replyTime;
                ems.ReplyVariance = variance;
                return true;
            });
            return original != newStates.ReplyTime;
        }
    }
}

