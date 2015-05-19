//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.SMTP
{
	interface ISmtpCommand
	{
        void Execute (NcStateMachine sm);
        void Cancel ();
	}

}

