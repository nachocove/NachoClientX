//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

namespace NachoCore.Model
{
	public class NcMigration59 : NcMigration
	{

		public override int GetNumberOfObjects ()
		{
			return 1;
		}

		public override void Run (System.Threading.CancellationToken token)
		{
			NcModel.Instance.Db.Execute ("DROP TABLE McEmailMessageDependency");
		}
	}
}
