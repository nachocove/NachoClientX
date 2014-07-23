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
            NcAssert.True (asResult.isOK (), "asResult.isOK");
            NcAssert.NotNull (asContact, "asContact");

            // Convert the AsContact to an McContact
            var mcResult = asContact.ToMcContact (folder.AccountId);
            var mcContact = mcResult.GetValue<McContact> ();
            NcAssert.True (mcResult.isOK ());
            NcAssert.NotNull (mcContact, "mcContact");

            var existingContact = McAbstrFolderEntry.QueryByServerId<McContact> (folder.AccountId, mcContact.ServerId);

            if (null == existingContact) {
                var ur = mcContact.Insert ();
                folder.Link (mcContact);
                NcAssert.True (0 < ur, "mcContact.Insert");
            } else {
                mcContact.Id = existingContact.Id;
                var ur = mcContact.Update ();
                NcAssert.True (0 < ur, "mcContact.Update");
            }
        }
    }
}

