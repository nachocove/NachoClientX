//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoClient.Build;

namespace NachoCore.Utils
{
    public class BuildInfoHelper
    {
        public static bool IsBeta {
            get {
                return "beta" == BuildInfo.AwsPrefix;
            }
        }

        public static bool IsAlpha {
            get {
                return "alpha" == BuildInfo.AwsPrefix;
            }
        }

        public static bool IsDev {
            get {
                return "dev" == BuildInfo.AwsPrefix;
            }
        }
    }
}

