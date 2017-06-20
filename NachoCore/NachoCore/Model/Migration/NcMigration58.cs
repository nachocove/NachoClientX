//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

namespace NachoCore.Model
{
    public class NcMigration58 : NcMigration
    {

        public override int GetNumberOfObjects ()
        {
            return 1;
        }

        public override void Run (System.Threading.CancellationToken token)
        {
            NcModel.Instance.Db.Execute ("UPDATE McFolder SET FilterSetting = 0 WHERE FilterSetting = 2");
            var unified = new NachoUnifiedInbox ();
            if (unified.FilterSetting == FolderFilterOptions.Focused){
                unified.FilterSetting = FolderFilterOptions.All;
            }
        }
    }
}
