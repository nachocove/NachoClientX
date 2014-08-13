﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    public enum NcBrainEventType {
        UNKNOWN = 0,
        PERIODIC_GLEAN,
        UI,
        STATE_MACHINE,
        TERMINATE,
        TEST,
        MESSAGE_FLAGS,
        INITIAL_RIC,
        UPDATE_ADDRESS_SCORE,
        UPDATE_MESSAGE_SCORE
    };

    public class NcBrainEvent : NcQueueElement
    {
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

    public enum NcBrainUIEventType {
        MESSAGE_VIEW
    };

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

    /// This event is used when an UI / backend action causes the score of 
    /// an email message to be updated.
    public class NcBrainUpdateMessageScoreEvent : NcBrainEvent
    {
        public Int64 EmailMessageId;

        public NcBrainUpdateMessageScoreEvent (Int64 emailMessageId)
            : base (NcBrainEventType.UPDATE_MESSAGE_SCORE)
        {
            EmailMessageId = emailMessageId;
        }

        public override string ToString ()
        {
            return String.Format ("[NcBrainUpdateMessageScoreEvent: type={0}, emailMessageId={1}",
                GetEventType (), EmailMessageId);
        }
    }

    /// This event tells brain that user has changed either the due date or the deferred until date.
    /// Upon receiving this, brain will re-evaluate the time variance state machine for the
    /// email message
    public class NcBrainMessageFlagEvent : NcBrainEvent
    {
        public Int64 AccountId;
        public Int64 EmailMessageId;

        public NcBrainMessageFlagEvent (Int64 accountId, Int64 emailMessageId) : base (NcBrainEventType.MESSAGE_FLAGS)
        {
            AccountId = accountId;
            EmailMessageId = emailMessageId;
        }

        public override string ToString ()
        {
            return String.Format ("[NcBrainMessageFlagEvent: type={0}, accountId={1}, emailMessageId={2}]",
                GetEventType (), AccountId, EmailMessageId);
        }
    }
}

