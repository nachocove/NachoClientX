using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public partial class AsFolderSyncCommand : AsCommand
    {
        private bool HadFolderChanges;

        public AsFolderSyncCommand (IBEContext beContext) :
            base (Xml.FolderHierarchy.FolderSync, Xml.FolderHierarchy.Ns, beContext)
        {
            SuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_FolderSyncSucceeded);
            FailureInd = NcResult.Error (NcResult.SubKindEnum.Error_FolderSyncFailed);
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var syncKey = BEContext.ProtocolState.AsSyncKey;
            Log.Info (Log.LOG_AS, "{0}: AsSyncKey={1}", CmdNameWithAccount, syncKey);
            var folderSync = new XElement (m_ns + Xml.FolderHierarchy.FolderSync, new XElement (m_ns + Xml.FolderHierarchy.SyncKey, syncKey));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (folderSync);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "FSYNCCANCEL");
            }
            McProtocolState protocolState = BEContext.ProtocolState;
            var status = (Xml.FolderHierarchy.FolderSyncStatusCode)Convert.ToUInt32 (doc.Root.Element (m_ns + Xml.FolderHierarchy.Status).Value);
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.AsLastFolderSync = DateTime.UtcNow;
                return true;
            });

            switch (status) {

            case Xml.FolderHierarchy.FolderSyncStatusCode.Success_1:
                var syncKey = doc.Root.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                Log.Info (Log.LOG_AS, "{0}: process response: SyncKey={1}", CmdNameWithAccount, syncKey);
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.AsSyncKey = syncKey;
                    return true;
                });
                var changes = doc.Root.Element (m_ns + Xml.FolderHierarchy.Changes).Elements ();
                if (null != changes) {
                    foreach (var change in changes) {
                        string serverId, parentId;
                        McPath pathElem;
                        switch (change.Name.LocalName) {
                        case Xml.FolderHierarchy.Add:
                            HadFolderChanges = true;
                            serverId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                            if (McFolder.GMail_All_ServerId == serverId) {
                                Log.Info (Log.LOG_AS, "{0}: Ignoring GMail folder {1}.", CmdNameWithAccount, serverId);
                                break;
                            }
                            parentId = change.Element (m_ns + Xml.FolderHierarchy.ParentId).Value;
                            pathElem = new McPath (AccountId);
                            pathElem.ServerId = serverId;
                            pathElem.ParentId = parentId;
                            pathElem.IsFolder = true;
                            pathElem.Insert ();
                            var applyAdd = new ApplyFolderAdd (AccountId) {
                                ServerId = serverId, 
                                ParentId = parentId,
                                DisplayName = change.Element (m_ns + Xml.FolderHierarchy.DisplayName).Value,
                                FolderType = (Xml.FolderHierarchy.TypeCode)uint.Parse (change.Element (m_ns + Xml.FolderHierarchy.Type).Value),
                            };
                            applyAdd.ProcessServerCommand ();
                            break;
                        case Xml.FolderHierarchy.Update:
                            HadFolderChanges = true;
                            serverId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                            parentId = change.Element (m_ns + Xml.FolderHierarchy.ParentId).Value;
                            pathElem = McPath.QueryByServerId (AccountId, serverId);
                            pathElem.ParentId = parentId;
                            pathElem.Update ();
                            var applyUpdate = new ApplyFolderUpdate (AccountId) {
                                ServerId = serverId,
                                ParentId = parentId,
                                DisplayName = change.Element (m_ns + Xml.FolderHierarchy.DisplayName).Value,
                                FolderType = uint.Parse (change.Element (m_ns + Xml.FolderHierarchy.Type).Value),
                            };
                            applyUpdate.ProcessServerCommand ();
                            break;
                        case Xml.FolderHierarchy.Delete:
                            HadFolderChanges = true;
                            serverId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                            var applyDelete = new ApplyFolderDelete (AccountId) {
                                ServerId = serverId,
                            };
                            applyDelete.ProcessServerCommand ();
                            // The path information can't be deleted until *after* conflict analysis is complete.
                            pathElem = McPath.QueryByServerId (AccountId, serverId);
                            pathElem.Delete ();
                            break;
                        }
                    }
                }

                if (protocolState.AsFolderSyncEpochScrubNeeded) {
                    PerformFolderSyncEpochScrub ();
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.AsFolderSyncEpochScrubNeeded = false;
                        return true;
                    });
                }

                if (HadFolderChanges) {
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
                    ((AsProtoControl)BEContext.ProtoControl).ResetFolderReSyncCount ();
                }
                return Event.Create ((uint)SmEvt.E.Success, "FSYNCSUCCESS");

            case Xml.FolderHierarchy.FolderSyncStatusCode.Retry_6:
            case Xml.FolderHierarchy.FolderSyncStatusCode.Unknown_11:
                // TODO: we need to count loops, possibly delay, and if bad then set key to 0 and try again.
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FSYNCAGAIN");

            case Xml.FolderHierarchy.FolderSyncStatusCode.ReSync_9:
                // "Delete items added since last synchronization." <= Let conflict resolution deal with this.
                protocolState.IncrementAsFolderSyncEpoch ();
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.IncrementAsFolderSyncEpoch ();
                    return true;
                });
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FSYNCAGAIN2");

            case Xml.FolderHierarchy.FolderSyncStatusCode.ServerFail_12:
            case Xml.FolderHierarchy.FolderSyncStatusCode.BadFormat_10:
                return Event.Create ((uint)SmEvt.E.HardFail, "FSYNCBADFMT");

            default:
                Log.Error (Log.LOG_AS, "{0}: UNHANDLED status {1}", CmdNameWithAccount, status);
                return Event.Create ((uint)SmEvt.E.HardFail, "FSYNCHARD");
            }
        }

        public override void StatusInd (bool didSucceed)
        {
            if (didSucceed) {
                McPending.MakeEligibleOnFSync (AccountId);
            }
            base.StatusInd (didSucceed);
        }

        private void PerformFolderSyncEpochScrub ()
        {
            Log.Info (Log.LOG_AS, "{0}: PerformFolderSyncEpochScrub", CmdNameWithAccount);
            var laf = McFolder.GetLostAndFoundFolder (AccountId);
            var orphaned = McFolder.QueryByIsClientOwned (AccountId, false)
                .Where (x => x.AsFolderSyncEpoch < BEContext.ProtocolState.AsFolderSyncEpoch).ToList ();
            Log.Info (Log.LOG_AS, "{0}: PerformFolderSyncEpochScrub: {1} folders.", CmdNameWithAccount, orphaned.Count);
            foreach (var iterFolder in orphaned) {
                var folder = iterFolder;
                Log.Info (Log.LOG_AS, "{0}: PerformFolderSyncEpochScrub: moving old folder {1} under LAF.", CmdNameWithAccount, folder.Id);
                // If an Add command from the server re-used this folder's ServerId, then
                // we changed that server id to a GUID when applying the Add to the model.
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ParentId = laf.ServerId;
                    target.IsClientOwned = true;
                    return true;
                });
            }
        }
    }
}
