﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SQLite;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class McTask : McItem
    {
        // FIXME - add categories.

        public bool Complete { set; get; }

        public DateTime DateCompleted { set; get; }

        public DateTime DueDate { get; set; }

        public NcImportance Importance { get; set; }

        public bool ReminderSet { get; set; }

        public DateTime ReminderTime { get; set; }

        public NcSensitivity Sensitivity { get; set; }

        public DateTime StartDate { get; set; }

        public string Subject { get; set; }

        public DateTime UtcDueDate { get; set; }

        public DateTime UtcStartDate { get; set; }

        /* FIXME - figure out how these work.
        public uint Recurrence_DeadOccur { set; get; }

        public bool Recurrence_Regenerate { get; set; }
         */

        public McTask ()
        {
            DateCompleted = DateTime.MinValue;
            DueDate = DateTime.MinValue;
            ReminderTime = DateTime.MinValue;
            StartDate = DateTime.MinValue;
            UtcDueDate = DateTime.MinValue;
            UtcStartDate = DateTime.MinValue;
        }

        public McRecurrence GetRecurrence ()
        {
            return BackEnd.Instance.Db.Table<McRecurrence> ().SingleOrDefault (x => x.TaskId == Id);
        }

        public static ClassCodeEnum GetClassCode ()
        {
            return McFolderEntry.ClassCodeEnum.Tasks;
        }

        public XElement ToXmlApplicationData ()
        {
            XNamespace AirSyncNs = Xml.AirSync.Ns;
            XNamespace AirSyncBaseNs = Xml.AirSyncBase.Ns;
            XNamespace TaskNs = Xml.Tasks.Ns;

            var xmlAppData = new XElement (AirSyncNs + Xml.AirSync.ApplicationData);

            if (0 != BodyId) {
                var body = McBody.QueryById<McBody> (BodyId);
                NachoAssert.True (null != body);
                xmlAppData.Add (new XElement (AirSyncBaseNs + Xml.AirSyncBase.Body,
                    // FIXME - need to have dynamic Body type.
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Type, (uint)Xml.AirSync.TypeCode.PlainText_1),
                    new XElement (AirSyncBaseNs + Xml.AirSyncBase.Data, body.Body)));
            }
            xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.Complete, Convert.ToUInt32 (Complete)));
            if (DateTime.MinValue != DateCompleted) {
                xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.DateCompleted, 
                    DateCompleted.ToString (AsHelpers.DateTimeFmt1)));
            }
            if (DateTime.MinValue != DueDate) {
                xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.DueDate,
                    DueDate.ToString (AsHelpers.DateTimeFmt1)));
            }
            xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.Importance, (uint)Importance));
            if (ReminderSet) {
                xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.ReminderSet, 1));
                NachoAssert.True (DateTime.MinValue != ReminderTime);
                xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.ReminderTime,
                    ReminderTime.ToString (AsHelpers.DateTimeFmt1)));
            }
            xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.Sensitivity, (uint)Sensitivity));
            if (DateTime.MinValue != StartDate) {
                xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.StartDate,
                    StartDate.ToString (AsHelpers.DateTimeFmt1)));
            }
            if (null != Subject) {
                xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.Subject, Subject));
            }
            if (DateTime.MinValue != UtcDueDate) {
                xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.UtcDueDate,
                    UtcDueDate.ToString (AsHelpers.DateTimeFmt1)));
            }

            if (DateTime.MinValue != UtcStartDate) {
                xmlAppData.Add (new XElement (TaskNs + Xml.Tasks.UtcStartDate,
                    UtcStartDate.ToString (AsHelpers.DateTimeFmt1)));
            }

            var recurrence = McRecurrence.QueryByTaskId (Id);
            if (null != recurrence) {
                // FIXME - express recurrence.
            }
            return xmlAppData;
        }

        public NcResult FromXmlApplicationData (XElement applicationData)
        {
            XNamespace baseNs = Xml.AirSyncBase.Ns;
            bool CompleteBeenSeen = false;
            bool DateCompletedSeen = false;
            foreach (var child in applicationData.Elements()) {
                switch (child.Name.LocalName) {
                case Xml.AirSyncBase.Body:
                    // FIXME - capture Type too.
                    var bodyElement = child.Element (baseNs + Xml.AirSyncBase.Data);
                    if (null != bodyElement) {
                        var saveAttr = bodyElement.Attributes ().SingleOrDefault (x => x.Name == "nacho-body-id");
                        if (null != saveAttr) {
                            BodyId = int.Parse (saveAttr.Value);
                        } else {
                            var body = new McBody ();
                            body.Body = bodyElement.Value; 
                            body.Insert ();
                            BodyId = body.Id;
                        }
                    } else {
                        BodyId = 0;
                        Console.WriteLine ("Task: Truncated message from server.");
                    }
                    break;

                case Xml.Tasks.Complete:
                    CompleteBeenSeen = true;
                    Complete = AsExtensions.ToBoolean (child.Value);
                    break;

                case Xml.Tasks.DateCompleted:
                    DateCompletedSeen = true;
                    DateCompleted = AsHelpers.ParseAsDateTime (child.Value);
                    break;

                case Xml.Tasks.DueDate:
                    DueDate = AsHelpers.ParseAsDateTime (child.Value);
                    break;

                case Xml.Tasks.Importance:
                    // FIXME - Importance can be > 2. kill the enum?
                    Importance = (NcImportance)uint.Parse (child.Value);
                    break;

                case Xml.Tasks.Recurrence:
                    // FIXME parse recurrence.
                    break;

                case Xml.Tasks.ReminderSet:
                    ReminderSet = AsExtensions.ToBoolean (child.Value);
                    break;

                case Xml.Tasks.ReminderTime:
                    ReminderTime = AsHelpers.ParseAsDateTime (child.Value);
                    break;

                case Xml.Tasks.Sensitivity:
                    Sensitivity = (NcSensitivity)uint.Parse (child.Value);
                    break;

                case Xml.Tasks.StartDate:
                    StartDate = AsHelpers.ParseAsDateTime (child.Value);
                    break;

                case Xml.Tasks.Subject:
                    Subject = child.Value;
                    break;

                case Xml.Tasks.UtcDueDate:
                    UtcDueDate = AsHelpers.ParseAsDateTime (child.Value);
                    break;

                case Xml.Tasks.UtcStartDate:
                    UtcStartDate = AsHelpers.ParseAsDateTime (child.Value);
                    break;

                default:
                    Log.Warn (Log.LOG_AS, "Unexpected element in Task/ApplicationData: {0}", child);
                    break;
                }
            }
            if (!CompleteBeenSeen ||
                Complete && !DateCompletedSeen) {
                return NcResult.Error ("Need XML Complete, and if Complete need DateCompleted.");
            }
            return NcResult.OK ();
        }
    }
}

