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
            string cmdNameWithAccount = string.Format ("AsSyncCommand({0})", folder.AccountId);
            var xmlServerId = command.Element (Ns + Xml.AirSync.ServerId);
            if (null == xmlServerId || null == xmlServerId.Value || string.Empty == xmlServerId.Value) {
                Log.Error (Log.LOG_AS, "{0}: ServerSaysAddOrChangeContact: No ServerId present.", cmdNameWithAccount);
                return;
            }
            // If the server attempts to overwrite, delete the pre-existing record first.
            var existingContact = McContact.QueryByServerId<McContact> (folder.AccountId, xmlServerId.Value);
            if (Xml.AirSync.Add == command.Name.LocalName && null != existingContact) {
                existingContact.Delete ();
                existingContact = null;
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
                Log.Error (Log.LOG_AS, "{0}: ServerSaysAddOrChangeContact: Exception parsing: {1}", cmdNameWithAccount, ex.ToString ());
                if (null == mcContact || null == mcContact.ServerId || string.Empty == mcContact.ServerId) {
                    mcContact = new McContact () {
                        ServerId = xmlServerId.Value,
                        Source = McAbstrItem.ItemSource.ActiveSync,
                    };
                }
                mcContact.AccountId = folder.AccountId;
                mcContact.IsIncomplete = true;
            }
            if (null == existingContact) {
                NcModel.Instance.RunInTransaction (() => {
                    var ur = mcContact.Insert ();
                    NcAssert.True (0 < ur, "mcContact.Insert");
                    folder.Link (mcContact);
                });
            } else {
                mcContact.Id = existingContact.Id;
                folder.UpdateLink (mcContact);
                var ur = mcContact.Update ();
                NcAssert.True (0 < ur, "mcContact.Update");
            }
        }
    }
}

