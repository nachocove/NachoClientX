//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public static class NcAbate
    {
        public static void HighPriority (string caller)
        {
            NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "HighPriority sent Info_BackgroundAbateStarted from {0}", caller);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStarted),
                Account = NachoCore.Model.ConstMcAccount.NotAccountSpecific,
                Tokens = new String[] { DateTime.Now.ToString () },
            });
            NcApplication.Instance.IsBackgroundAbateRequired = true;
            NachoCore.Model.NcModel.Instance.RateLimiter.Enabled = true;

        }

        public static void RegularPriority (string caller)
        {
            NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "RegularPriority sent Info_BackgroundAbateStopped from {0}", caller);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStopped),
                Account = NachoCore.Model.ConstMcAccount.NotAccountSpecific,
                Tokens = new String[] { DateTime.Now.ToString () },
            });
            NcApplication.Instance.IsBackgroundAbateRequired = false;
            NachoCore.Model.NcModel.Instance.RateLimiter.Enabled = false;
        }

        public static TimeSpan DeliveryTime (StatusIndEventArgs e)
        {
            string sentString = e.Tokens [0];
            var sentTime = DateTime.Parse (sentString);
            var deliveryTime = DateTime.Now - sentTime;
            return deliveryTime;
        }
    }
}

