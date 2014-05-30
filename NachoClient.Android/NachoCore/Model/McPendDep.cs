//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

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

        public static void DeleteAllSucc (int predId)
        {
            NcModel.Instance.Db.Execute ("DELETE FROM McPendDep WHERE PredId = ?", predId);
        }
    }
}

