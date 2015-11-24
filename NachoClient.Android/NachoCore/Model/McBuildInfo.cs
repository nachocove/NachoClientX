//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
// WARNING - this is a singleton object in the DB that is NOT a sub-class of McAbstrObject.
// it is used exclusively for migration decision making.

using System;

namespace NachoCore.Model
{
    public class McBuildInfo
    {
        public string Version { get; set; }
        public string BuildNumber { get; set; }
        public string Time { get; set; }
    }
}
