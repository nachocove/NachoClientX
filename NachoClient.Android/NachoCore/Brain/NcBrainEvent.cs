//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public enum NcBrainEventType
    {
        UNKNOWN = 0,
        PERIODIC_GLEAN,
        UI,
        STATE_MACHINE,
        TERMINATE,
        TEST,
        MESSAGE_FLAGS,
        INITIAL_RIC,
        UPDATE_ADDRESS_SCORE,
        UPDATE_MESSAGE_SCORE,
        UNINDEX_MESSAGE,
        UNINDEX_CONTACT,
        PERSISTENT_QUEUE,
        REINDEX_CONTACT,
        UPDATE_MESSAGE_NOTIFICATION_STATUS,
        UPDATE_MESSAGE_READ_STATUS,
        UPDATE_MESSAGE_REPLY_STATUS,
        PAUSE_OBSOLETE,
        INDEX_MESSAGE,
    };

    [Serializable]
    public class NcBrainEvent : NcQueueElement
    {
        public const int KNotSpecificAccountId = int.MaxValue;

        public NcBrainEventType Type { get; set; }

        public NcBrainEvent (NcBrainEventType type)
        {
            Type = type;
        }

        public string GetEventType ()
        {
            return Enum.GetName (typeof(NcBrainEventType), Type);
        }

        public override string ToString ()
        {
            return String.Format ("[NcBrainEvent: type={0}]", GetEventType ());
        }

        public uint GetSize ()
        {
            return 0;
        }
    }

    public enum NcBrainUIEventType
    {
        MESSAGE_VIEW,
    };

    [Serializable]
    public class NcBrainUIEvent : NcBrainEvent
    {

        public NcBrainUIEventType UIType;

        public NcBrainUIEvent (NcBrainUIEventType type) : base (NcBrainEventType.UI)
        {
            UIType = type;
        }

        public string GetUIType ()
        {
            return Enum.GetName (typeof(NcBrainUIEventType), UIType);
        }

        public override string ToString ()
        {
            return String.Format ("[NcBrainUIEvent: type={0}, uitype={1}]", GetEventType (), GetUIType ());
        }
    }

    [Serializable]
    public class NcBrainUIMessageViewEvent : NcBrainUIEvent
    {
        public DateTime Start;
        public DateTime End;

        public NcBrainUIMessageViewEvent (NcBrainUIEventType type, DateTime start, DateTime end) : base (type)
        {
            Start = start;
            End = end;
        }

        public override string ToString ()
        {
            return String.Format ("[NcBrainUIMessageViewEvent: type={0}, uitype={1}, start={2}, end={3}]",
                GetEventType (), GetUIType (), Start, End);
        }
    }

    [Serializable]
    public class NcBrainInitialRicEvent : NcBrainEvent
    {
        public Int64 AccountId;

        public NcBrainInitialRicEvent (Int64 accountId) : base (NcBrainEventType.INITIAL_RIC)
        {
            AccountId = accountId;
        }

        public override string ToString ()
        {
            return String.Format ("[NcBrainInitialRicEvent: type={0}, accountId={1}]", GetEventType (), AccountId);
        }
    }

    /// This event is used when an UI / backend action causes the score of 
    /// an email address to be updated.
    [Serializable]
    public class NcBrainUpdateAddressScoreEvent : NcBrainEvent
    {
        public Int64 EmailAddressId;

        /// Normally, dependent messages are only updated if the address score changes.
        /// Setting this flag to true causes dependent email message scores to be 
        /// updated regardless.
        public bool ForceUpdateDependentMessages;

        public NcBrainUpdateAddressScoreEvent (Int64 emailAddressId, bool forcedUpdateDependentMessages)
            : base (NcBrainEventType.UPDATE_ADDRESS_SCORE)
        {
            EmailAddressId = emailAddressId;
            ForceUpdateDependentMessages = forcedUpdateDependentMessages;
        }

        public override string ToString ()
        {
            return String.Format ("[NcBrainUpdateAddressScoreEvent: type={0}, emailAddressId={1}",
                GetEventType (), EmailAddressId);
        }
    }

    [Serializable]
    public class NcBrainMessageEvent : NcBrainEvent
    {
        public Int64 AccountId;

        public Int64 EmailMessageId;

        public NcBrainMessageEvent (NcBrainEventType eventType, Int64 accountId, Int64 emailMessageId)
            : base (eventType)
        {
            AccountId = accountId;
            EmailMessageId = emailMessageId;
        }

        public override string ToString ()
        {
            return String.Format ("[{0}: type={1}, accountId={2}, emailMessageId={3}",
                GetType ().Name, Type, AccountId, EmailMessageId);
        }
    }

    /// This event is used when an UI / backend action causes the score of 
    /// an email message to be updated.
    [Serializable]
    public class NcBrainUpdateMessageScoreEvent : NcBrainMessageEvent
    {
        public NcBrainUpdateMessageScoreEvent (Int64 accountId, Int64 emailMessageId)
            : base (NcBrainEventType.UPDATE_MESSAGE_SCORE, accountId, emailMessageId)
        {
        }
    }

    [Serializable]
    public class NcBrainUpdateUserActionEvent : NcBrainUpdateMessageScoreEvent
    {
        public int Action;

        public NcBrainUpdateUserActionEvent (Int64 accountId, Int64 emailMessageId, int action)
            : base (accountId, emailMessageId)
        {
            Action = action;
        }
    }

    /// This event tells brain that user has changed either the due date or the deferred until date.
    /// Upon receiving this, brain will re-evaluate the time variance state machine for the
    /// email message
    [Serializable]
    public class NcBrainMessageFlagEvent : NcBrainMessageEvent
    {
        public NcBrainMessageFlagEvent (Int64 accountId, Int64 emailMessageId)
            : base (NcBrainEventType.MESSAGE_FLAGS, accountId, emailMessageId)
        {
        }
    }

    [Serializable]
    public class NcBrainIndexMessageEvent : NcBrainMessageEvent
    {
        public NcBrainIndexMessageEvent (Int64 accountId, Int64 emailMessageId)
            : base (NcBrainEventType.INDEX_MESSAGE, accountId, emailMessageId)
        {
        }
    }

    [Serializable]
    public class NcBrainUnindexMessageEvent : NcBrainMessageEvent
    {
        public NcBrainUnindexMessageEvent (Int64 accountId, Int64 emailMessageId)
            : base (NcBrainEventType.UNINDEX_MESSAGE, accountId, emailMessageId)
        {
        }
    }

    [Serializable]
    public class NcBrainUpdateMessageNotificationStatusEvent : NcBrainMessageEvent
    {
        public DateTime NotificationTime;

        public double Variance;

        public NcBrainUpdateMessageNotificationStatusEvent (Int64 accountId, Int64 emailMessageId)
            : base (NcBrainEventType.UPDATE_MESSAGE_NOTIFICATION_STATUS, accountId, emailMessageId)
        {
        }
    }

    [Serializable]
    public class NcBrainUpdateMessageReadStatusEvent : NcBrainMessageEvent
    {
        public DateTime ReadTime;

        public double Variance;

        public NcBrainUpdateMessageReadStatusEvent (Int64 accountId, Int64 emailMessageId)
            : base (NcBrainEventType.UPDATE_MESSAGE_READ_STATUS, accountId, emailMessageId)
        {
        }
    }

    [Serializable]
    public class NcBrainUpdateMessageReplyStatusEvent : NcBrainMessageEvent
    {
        public DateTime ReplyTime;

        public double Variance;

        public NcBrainUpdateMessageReplyStatusEvent (Int64 accountId, Int64 emailMessageId)
            : base (NcBrainEventType.UPDATE_MESSAGE_REPLY_STATUS, accountId, emailMessageId)
        {
        }
    }

    [Serializable]
    public class NcBrainContactEvent : NcBrainEvent
    {
        public Int64 AccountId;

        public Int64 ContactId;

        public NcBrainContactEvent (NcBrainEventType eventType, Int64 accountId, Int64 contactId)
            : base (eventType)
        {
            AccountId = accountId;
            ContactId = contactId;
        }


        public override string ToString ()
        {
            return String.Format ("[{0}: type={1}, accountId={2}, ContactId={3}",
                GetType ().Name, Type, AccountId, ContactId);
        }
    }

    [Serializable]
    public class NcBrainUnindexContactEvent : NcBrainContactEvent
    {
        public NcBrainUnindexContactEvent (Int64 accountId, Int64 contactId)
            : base (NcBrainEventType.UNINDEX_CONTACT, accountId, contactId)
        {
        }
    }

    [Serializable]
    public class NcBrainReindexContactEvent : NcBrainContactEvent
    {
        public NcBrainReindexContactEvent (Int64 accountId, Int64 contactId)
            : base (NcBrainEventType.REINDEX_CONTACT, accountId, contactId)
        {
        }
    }

    [Serializable]
    public class NcBrainStateMachineEvent : NcBrainEvent
    {
        public Int64 AccountId;
        public int Count;

        public NcBrainStateMachineEvent (Int64 accountId, int count) : base (NcBrainEventType.STATE_MACHINE)
        {
            AccountId = accountId;
            Count = count;
        }

        public override string ToString ()
        {
            return String.Format ("[NcBrainStateMachineEvent: type={0} accountId={1}", GetEventType (), AccountId);
        }
    }

    // This event is for kickstart processing in the persistent queue. Do not insert this into db.
    public class NcBrainPersistentQueueEvent : NcBrainEvent
    {
        public NcBrainPersistentQueueEvent () : base (NcBrainEventType.PERSISTENT_QUEUE)
        {
        }

        public override string ToString ()
        {
            return String.Format ("[NcBrainPersistentQueueEvent: type={0}", GetEventType ());
        }
    }
}

