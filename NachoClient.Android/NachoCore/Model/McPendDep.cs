//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McPendDep : McAbstrObjectPerAcc
    {
        public int PredId { get; set; }
        public int SuccId { get; set; }

        public McPendDep ()
        {
        }

        public McPendDep (int accountId, int predId, int succId) : this ()
        {
            AccountId = accountId;
            PredId = predId;
            SuccId = succId;
        }

        public static IEnumerable<McPendDep> QueryBySuccId (int succId)
        {
            return NcModel.Instance.Db.Query<McPendDep> ("SELECT * FROM McPendDep WHERE SuccId = ?", succId);
        }

        public static void DeleteAllSucc (int predId)
        {
            NcModel.Instance.BusyProtect (() => {
                NcModel.Instance.Db.Execute ("DELETE FROM McPendDep WHERE PredId = ?", predId);
                return 1;
            });
        }

        public static List<McPendDep> QueryByPredId (int predId)
        {
            return NcModel.Instance.Db.Query<McPendDep> ("SELECT f.* FROM McPendDep AS f WHERE " +
                " f.PredId = ? ", 
                predId).ToList ();
        }
    }
}

