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
        public void ServerSaysAddContact (XElement command, NcFolder folder)
        {
            Log.Info (Log.LOG_CONTACTS, "ServerSaysAddContact\n{0}", command.ToString ());
            ProcessContactItem (command, folder);
        }

        public void ServerSaysChangeContact (XElement command, NcFolder folder)
        {
            Log.Info (Log.LOG_CONTACTS, "ServerSaysChangeContact\n{0}", command.ToString ());
            ProcessContactItem (command, folder);
        }

        public void ProcessContactItem (XElement command, NcFolder folder)
        {
            // Convert the XML to an NcContact
            var h = new AsHelpers ();
            var r = h.ParseContact (m_ns, command, folder);
            var newItem = (NcContact)r.GetObject ();

            System.Diagnostics.Trace.Assert (r.isOK ());
            System.Diagnostics.Trace.Assert (null != newItem);

            // Look up the event by ServerId
            NcContact oldItem = null;

            try {
                oldItem = DataSource.Owner.Db.Get<NcContact> (x => x.ServerId == newItem.ServerId);
            } catch (System.InvalidOperationException) {
                Log.Info (Log.LOG_CONTACTS, "ProcessContactItem: System.InvalidOperationException handled");
            } catch (Exception e) {
                Log.Info ("ProcessContactItem:\n{0}", e.ToString ());
            }

            // If there is no match, insert the new item.
            if (null == oldItem) {
                NcResult ir = DataSource.Owner.Db.Insert (newItem);
                System.Diagnostics.Trace.Assert (ir.isOK ());
                newItem.Id = ir.GetIndex ();
                return;
            }

            // Update existing item
            // Overwrite the old item with the new item
            // to preserve the index, in
            newItem.Id = oldItem.Id;
            NcResult ur = DataSource.Owner.Db.Update (oldItem);
            System.Diagnostics.Trace.Assert (ur.isOK ());

        }
    }
}

