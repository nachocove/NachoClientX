//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Linq;
using MimeKit;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.Brain
{

    public class NcContactGleaner
    {

        public static void GleanContacts (string address, int accountId)
        {
            if (Mailbox.TryParseArray (address, out var mailboxes)) {
                var gleanedFolder = McFolder.GetGleanedFolder (accountId);
                foreach (var mailbox in mailboxes) {
                    McContact.CreateFromMailboxIfNeeded (gleanedFolder, mailbox, out var contact);
                }
            }
        }
    }
}
