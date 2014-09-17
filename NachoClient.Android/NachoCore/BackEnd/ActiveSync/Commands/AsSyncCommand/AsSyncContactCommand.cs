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
        public static void ServerSaysAddOrChangeContact (XElement command, McFolder folder)
        {
            var xmlServerId = command.Element (Ns + Xml.AirSync.ServerId);
            if (null == xmlServerId || null == xmlServerId.Value || string.Empty == xmlServerId.Value) {
                Log.Error (Log.LOG_AS, "ServerSaysAddOrChangeContact: No ServerId present.");
                return;
            }
            McContact mcContact = null;
            try {
                var asResult = AsContact.FromXML (folder.AccountId, Ns, command);
                var asContact = asResult.GetValue<AsContact> ();
                NcAssert.True (asResult.isOK (), "asResult.isOK");
                NcAssert.NotNull (asContact, "asContact");
                var mcResult = asContact.ToMcContact (folder.AccountId);
                mcContact = mcResult.GetValue<McContact> ();
                NcAssert.True (mcResult.isOK ());
                NcAssert.NotNull (mcContact, "mcContact");
            } catch (Exception ex) {
                Log.Error (Log.LOG_AS, "ServerSaysAddOrChangeContact: Exception parsing: {0}", ex.ToString ());
                if (null == mcContact || null == mcContact.ServerId || string.Empty == mcContact.ServerId) {
                    mcContact = new McContact () {
                        ServerId = xmlServerId.Value,
                    };
                }
                mcContact.AccountId = folder.AccountId;
                mcContact.IsIncomplete = true;
            }

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

