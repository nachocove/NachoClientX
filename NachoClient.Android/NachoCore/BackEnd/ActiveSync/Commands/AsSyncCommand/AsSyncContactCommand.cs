//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using System.IO;

namespace NachoCore.ActiveSync
{
    public partial class AsSyncCommand : AsCommand
    {
        public static void ServerSaysAddContact (XElement command, McFolder folder)
        {
            ProcessContactItem (command, folder, true);
        }

        public static void ServerSaysChangeContact (XElement command, McFolder folder)
        {
            ProcessContactItem (command, folder, false);
        }

        public static void ProcessContactItem (XElement command, McFolder folder, bool isAdd)
        {
            // Convert the XML to an AsContact
            var asResult = AsContact.FromXML (Ns, command);
            var asContact = asResult.GetValue<AsContact> ();
            NachoCore.NachoAssert.True (asResult.isOK ());
            NachoCore.NachoAssert.True (null != asContact);

            // Convert the AsContact to an McContact
            var mcResult = asContact.ToMcContact ();
            var mcContact = mcResult.GetValue<McContact> ();
            NachoCore.NachoAssert.True (mcResult.isOK ());
            NachoCore.NachoAssert.True (null != mcContact);

            mcContact.AccountId = folder.AccountId;

            var existingContact = McFolderEntry.QueryByServerId<McContact> (folder.AccountId, mcContact.ServerId);

            if (null == existingContact) {
                var ur = mcContact.Insert ();
                folder.Link (mcContact);
                NachoCore.NachoAssert.True (0 < ur);
            } else {
                mcContact.Id = existingContact.Id;
                var ur = mcContact.Update ();
                NachoCore.NachoAssert.True (0 < ur);
            }
        }
    }
}

