//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NUnit.Framework;

namespace Test.iOS
{
    public class CommonProtoControlOps : CommonTestOps
    {
        public static McAccount CreateAccount (int pcsId = 5)
        {
            var account = new McAccount () {
                EmailAddr = "johnd@foo.utopiasystems.net",
                ServerId = 1,
                ProtocolStateId = pcsId,
            };
            account.Insert ();

            return account;
        }

        public static McProtocolState CreateProtocolState ()
        {
            McProtocolState pcs = new McProtocolState ();
            pcs.Insert ();

            return pcs;
        }

        public static AsProtoControl CreateProtoControl (int accountId = defaultAccountId)
        {
            // clean static property
            MockOwner.Status = null;

            MockOwner mockOwner = new MockOwner ();

            var pcs = CreateProtocolState ();
            CreateAccount (pcs.Id);
            NcTask.StartService ();

            AsProtoControl protoControl = new AsProtoControl (mockOwner, accountId);

            return protoControl;
        }
    }

    public class CommonFolderOps : CommonTestOps
    {
        public const string defaultServerId = "5";

        public static T CreateUniqueItem<T> (int accountId = defaultAccountId, string serverId = defaultServerId) where T : McItem, new ()
        {
            T newItem = new T ();
            newItem.AccountId = accountId;
            newItem.ServerId = serverId;
            newItem.Insert ();
            return newItem;
        }

        public static McFolder CreateFolder (int accountId, bool isClientOwned = false, bool isHidden = false, string parentId = "0", 
            string serverId = defaultServerId, string name = "Default name", NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode typeCode = NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1,
            bool isAwaitingDelete = false, bool isAwaitingCreate = false, bool autoInsert = true, string asSyncKey = "-1", 
            bool syncMetaToClient = false)
        {
            McFolder folder = McFolder.Create (accountId, isClientOwned, isHidden, parentId, serverId, name, typeCode);

            folder.IsAwaitingDelete = isAwaitingDelete;
            folder.IsAwaitingCreate = isAwaitingCreate;

            if (asSyncKey == "-1") {
                asSyncKey = null;
            } else {
                folder.AsSyncKey = asSyncKey;
            }
            folder.AsSyncMetaToClientExpected = syncMetaToClient;

            if (autoInsert) { folder.Insert (); }
            return folder;
        }
    }

    public class CommonTestOps
    {
        public const int defaultAccountId = 1;

        public void SetUp ()
        {
            NcModel.Instance.Reset (System.IO.Path.GetTempFileName ());
           
            // turn off telemetry logging for tests
            LogSettings settings = Log.SharedInstance.Settings;
            settings.Error.DisableTelemetry ();
            settings.Warn.DisableTelemetry ();
            settings.Info.DisableTelemetry ();
            settings.Debug.DisableTelemetry ();
        }

        public void TestForNachoExceptionFailure (Action action, string message)
        {
            try {
                action ();
                Assert.Fail (message);
            } catch (NachoCore.Utils.NcAssert.NachoAssertionFailure e) {
                Log.Info (Log.LOG_TEST, "NachoAssertFailure message: {0}", e.Message);
            }
        }
    }
}

