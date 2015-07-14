//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using SQLite;
using NachoCore.Index;
using NachoCore.Brain;

namespace NachoCore.Model
{
    public class NcMigration34 : NcMigration
    {
        protected TableQuery<McEmailMessage> IndexedEmailMessages ()
        {
            return Db.Table<McEmailMessage> ().Where ((e) => (e.IsIndexed > 0) /*&& (e.IsIndexed < EmailMessageIndexDocument.Version)*/);
        }

        public override int GetNumberOfObjects ()
        {
            return IndexedEmailMessages ().Count ();
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            NcIndex index;
            var indexes = new Dictionary<int, NcIndex> ();
            foreach (var emailMessage in IndexedEmailMessages ()) {
                token.ThrowIfCancellationRequested ();
                if (!indexes.TryGetValue (emailMessage.AccountId, out index)) {
                    index = NcBrain.SharedInstance.Index (emailMessage.AccountId);
                    indexes.Add (emailMessage.AccountId, index);
                    if (null != index) {
                        index.BeginRemoveTransaction ();
                    }
                }
                if (null == index) {
                    continue;
                }
                index.BatchRemove ("message", emailMessage.Id.ToString ());
                UpdateProgress (1);
            }
            foreach (var curIndex in indexes.Values) {
                curIndex.EndRemoveTransaction ();
            }
        }
    }
}

