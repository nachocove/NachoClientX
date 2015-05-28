//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NachoCore.Utils;
using System.Threading;
using MimeKit;
using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using MailKit.Security;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.IMAP
{
    public class ImapCommand : IImapCommand
    {
        protected IBEContext BEContext;
        protected ImapClient Client { get; set; }
        public CancellationTokenSource Cts { get; protected set; }

        public ImapCommand (IBEContext beContext, ImapClient imap)
        {
            Cts = new CancellationTokenSource ();
            BEContext = beContext;
            Client = imap;
        }

        public virtual void Execute (NcStateMachine sm)
        {
        }

        public virtual void Cancel ()
        {
            Cts.Cancel ();
        }
    }
}
