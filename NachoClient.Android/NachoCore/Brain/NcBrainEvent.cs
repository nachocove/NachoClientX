//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
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
        TERMINATE
    };

    public class NcBrainEvent : NcQueueElement
    {
        public NcBrainEventType Type { get; set; }

        public NcBrainEvent (NcBrainEventType type)
        {
            Type = type;
        }

        public override string ToString ()
        {
            return String.Format ("<NcBrainEvent: type={0}>", Enum.GetName (typeof(NcBrainEventType), Type));
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
        public DateTime Start;
        public DateTime End;

        public NcBrainUIEvent (NcBrainUIEventType type, DateTime start, DateTime end) : base (NcBrainEventType.UI)
        {
            UIType = type;
            Start = start;
            End = end;
        }

        public override string ToString ()
        {
            return String.Format ("<NcBrainUIEvent: type={0}, start={1}, end={2}>",
                Enum.GetName (typeof(NcBrainUIEventType), Type), Start, End);
        }
    }
}

