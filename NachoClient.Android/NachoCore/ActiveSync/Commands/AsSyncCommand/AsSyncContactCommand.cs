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
        public void ServerSaysAddContact (XElement command, McFolder folder)
        {
            Log.Info (Log.LOG_CONTACTS, "ServerSaysAddContact\n{0}", command);
            ProcessContactItem (command, folder);
        }

        public void ServerSaysChangeContact (XElement command, McFolder folder)
        {
            Log.Info (Log.LOG_CONTACTS, "ServerSaysChangeContact\n{0}", command);
            ProcessContactItem (command, folder);
        }

        public void ProcessContactItem (XElement command, McFolder folder)
        {
            // Convert the XML to an AsContact
            var asResult = AsContact.FromXML (m_ns, command);
            var asContact = asResult.GetValue<AsContact> ();
            NachoCore.NachoAssert.True (asResult.isOK ());
            NachoCore.NachoAssert.True (null != asContact);

            // Convert the AsContact to an McContact
            var mcResult = asContact.ToMcContact (folder);
            var mcContact = mcResult.GetValue<McContact> ();
            NachoCore.NachoAssert.True (mcResult.isOK ());
            NachoCore.NachoAssert.True (null != mcContact);

            // TODO: Do we have to ghost or merge here?

            var ur = mcContact.Insert (DataSource.Owner.Db);
            NachoCore.NachoAssert.True (ur.isOK ());
        }
    }
}

