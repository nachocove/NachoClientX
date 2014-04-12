//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public partial class AsFolderSyncCommand : AsCommand
    {
        private class ApplyFolderDelete : AsApplyServerDelta
        {
            public string ServerId { set; get; }

            public ApplyFolderDelete (int accountId)
                    : base (accountId)
            {
            }

            protected override List<McPending.ReWrite> ApplyDeltaToPending (McPending pending,
                out McPending.DbActionEnum action,
                out bool cancelDelta)
            {
                action = McPending.DbActionEnum.DoNothing;
                cancelDelta = false;
                switch (pending.Operation) {
                case McPending.Operations.FolderCreate:
                    if (pending.FolderCompletelyDominates (ServerId)) {
                        // Perform the FolderCreate, but under lost+found.
                        var folder = McFolder.Create (AccountId,
                            true,
                            false,
                            McFolder.ClientOwned_LostAndFound,
                            pending.ServerId,
                            pending.DisplayName,
                            pending.FolderType);
                        folder.Insert ();
                        // Delete the FolderCreate.
                        action = McPending.DbActionEnum.Delete;
                        cancelDelta = false;
                        // The re-write is to make everything dominated by the 
                        // FolderCreate folder client-owned.
                        return new List<McPending.ReWrite> () {
                            new McPending.ReWrite () {
                                IsMatch = (subject) => 
                                    pending.FolderCompletelyDominates (ServerId),
                                PerformReWrite = (subject) => {
                                    // Add new re-write. CAN WE ADD A REWRITE IN A REWRITE?
                                    // Move to LAF.
                                    // Do the Op now.
                                    return McPending.DbActionEnum.Delete;
                                },
                            }
                        };
                    }
                    break;

                case McPending.Operations.FolderDelete:
                    // If ServerID matches, then the pending is redundant, and the client has already
                    // performed the delete in the DB as well, so we are done.
                    if (pending.ServerId == ServerId) {
                        action = McPending.DbActionEnum.Delete;
                        cancelDelta = true;
                        return null;
                    }
                    // FIXME - there is still a dominates case here.
                    break;

                case McPending.Operations.FolderUpdate:
                    break;
                }
                return null;
            }

            protected override void ApplyDeltaToModel ()
            {
                // Remove the folder and anything subordinate.
                var folder = McFolderEntry.QueryByServerId<McFolder> (AccountId, ServerId);
                folder.Delete ();
            }
        }
    }
}
