//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Utils;

namespace NachoCore.Imap
{
    public partial class ImProtoControl : ProtoControl, IPushAssistOwner
    {
        public ImProtoControl (IProtoControlOwner owner, int accountId) : base (owner, accountId)
        {
            ProtoControl = this;
            // TODO decouple disk setup from constructor.
            EstablishService ();
            Sm = new NcStateMachine ("IMPC") {
            };
            Sm.Validate ();
            Sm.State = ProtocolState.ProtoControlState;
            SyncStrategy = new ImStrategy (this);
            PushAssist = new PushAssist (this);
                
        }

        private void EstablishService ()
        {
        }

        public ImProtoControl ProtoControl { set; get; }

        public IImStrategy SyncStrategy { set; get; }

        private PushAssist PushAssist { set; get; }

        // PushAssist support.
        public PushAssistParameters PushAssistParameters ()
        {
            return null; // should never happen
        }
    }
}

