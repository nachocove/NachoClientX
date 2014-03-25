using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public partial class AsFolderSyncCommand : AsCommand
    {
        private bool HadFolderChanges;

        public AsFolderSyncCommand (IBEContext dataSource) :
            base (Xml.FolderHierarchy.FolderSync, Xml.FolderHierarchy.Ns, dataSource)
        {
            SuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_FolderSyncSucceeded);
            FailureInd = NcResult.Error (NcResult.SubKindEnum.Error_FolderSyncFailed);
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var syncKey = BEContext.ProtocolState.AsSyncKey;
            Log.Info ("AsFolderSyncCommand: AsSyncKey=" + syncKey);
            var folderSync = new XElement (m_ns + Xml.FolderHierarchy.FolderSync, new XElement (m_ns + Xml.FolderHierarchy.SyncKey, syncKey));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (folderSync);
            Log.Info (Log.LOG_SYNC, "AsFolderSyncCommand:\n{0}", doc);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            McProtocolState protocolState = BEContext.ProtocolState;
            var status = (Xml.FolderHierarchy.FolderSyncStatusCode)Convert.ToUInt32 (doc.Root.Element (m_ns + Xml.FolderHierarchy.Status).Value);

            switch (status) {
            case Xml.FolderHierarchy.FolderSyncStatusCode.Success_1:
                var syncKey = doc.Root.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                Log.Info ("AsFolderSyncCommand process response: SyncKey=" + syncKey);
                Log.Info (Log.LOG_SYNC, "AsFolderSyncCommand response:\n{0}", doc);
                protocolState.AsSyncKey = syncKey;
                protocolState.Update ();
                var changes = doc.Root.Element (m_ns + Xml.FolderHierarchy.Changes).Elements ();
                if (null != changes) {
                    HadFolderChanges = true;
                    // Q: should we try-block each op, since we are taking the syncKey up front?
                    foreach (var change in changes) {
                        switch (change.Name.LocalName) {
                        case Xml.FolderHierarchy.Add:
                            var applyAdd = new ApplyFolderAdd (BEContext.Account.Id) {
                                ServerId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value, 
                                ParentId = change.Element (m_ns + Xml.FolderHierarchy.ParentId).Value,
                                DisplayName = change.Element (m_ns + Xml.FolderHierarchy.DisplayName).Value,
                                FolderType = uint.Parse (change.Element (m_ns + Xml.FolderHierarchy.Type).Value),
                            };
                            applyAdd.ProcessDelta ();
                            break;
                        case Xml.FolderHierarchy.Update:
                            var applyUpdate = new ApplyFolderUpdate (BEContext.Account.Id) {
                                ServerId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value,
                                ParentId = change.Element (m_ns + Xml.FolderHierarchy.ParentId).Value,
                                DisplayName = change.Element (m_ns + Xml.FolderHierarchy.DisplayName).Value,
                                FolderType = uint.Parse (change.Element (m_ns + Xml.FolderHierarchy.Type).Value),
                            };
                            applyUpdate.ProcessDelta ();
                            break;
                        case Xml.FolderHierarchy.Delete:
                            var applyDelete = new ApplyFolderDelete (BEContext.Account.Id) {
                                ServerId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value,
                            };
                            applyDelete.ProcessDelta ();
                            break;
                        }
                    }
                }
                // FIXME we may need to indicate that a Sync is needed.
                if (HadFolderChanges) {
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
                }
                return Event.Create ((uint)SmEvt.E.Success, "FSYNCSUCCESS");

            case Xml.FolderHierarchy.FolderSyncStatusCode.Retry_6:
            case Xml.FolderHierarchy.FolderSyncStatusCode.Unknown_11:
                // TODO: we need to count loops, possibly delay, and if bad then set key to 0 and try again.
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FSYNCAGAIN");

            case Xml.FolderHierarchy.FolderSyncStatusCode.ReSync_9:
                // TODO: Delete items added since last synchronization.
                protocolState.AsSyncKey = McProtocolState.AsSyncKey_Initial;
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FSYNCAGAIN2");

            case Xml.FolderHierarchy.FolderSyncStatusCode.ServerFail_12:
            case Xml.FolderHierarchy.FolderSyncStatusCode.BadFormat_10:
                return Event.Create ((uint)SmEvt.E.HardFail, "FSYNCBADFMT");

            default:
                Log.Error ("ASFoldersyncCommand: UNHANDLED status {0}", status);
                return Event.Create ((uint)SmEvt.E.HardFail, "FSYNCHARD");
            }
        }

        public override void StatusInd (bool didSucceed)
        {
            if (didSucceed) {
                McPending.MakeEligibleOnFSync (BEContext.Account.Id);
            }
            base.StatusInd (didSucceed);
        }
    }
}
