//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;

namespace NachoCore.Model
{
    public class McPendDep : McObject
    {
        public int PredId { get; set; }
        public int SuccId { get; set; }

        public McPendDep ()
        {
        }

        public McPendDep (int predId, int succId)
        {
            PredId = predId;
            SuccId = succId;
        }

        public static IEnumerable<McPendDep> QueryBySuccId (int succId)
        {
            return NcModel.Instance.Db.Query<McPendDep> ("SELECT * FROM McPendDep WHERE SuccId = ?", succId);
        }

        public static void DeleteAllSucc (int predId)
        {
            NcModel.Instance.Db.Execute ("DELETE FROM McPendDep WHERE PredId = ?", predId);
        }

        public static List<McPendDep> QueryByPredId (int predId)
        {
            return NcModel.Instance.Db.Query<McPendDep> ("SELECT f.* FROM McPendDep AS f WHERE " +
                " f.PredId = ? ", 
                predId).ToList ();
        }
    }
}

