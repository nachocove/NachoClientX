//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public static class NcAbate
    {
        public static void HighPriority (string caller)
        {
            if (NcApplication.Instance.IsBackgroundAbateRequired) {
                return; // Squelch the extra status inds
            }
            NcApplication.Instance.IsBackgroundAbateRequired = true;
            NachoCore.Model.NcModel.Instance.RateLimiter.Enabled = true;
            // NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "HighPriority sent Info_BackgroundAbateStarted from {0}", caller);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStarted),
                Account = NachoCore.Model.ConstMcAccount.NotAccountSpecific,
                Stamp = DateTime.UtcNow,
            });

        }

        public static void RegularPriority (string caller)
        {
            if (!NcApplication.Instance.IsBackgroundAbateRequired) {
                return; // Squelch the extra status inds
            }
            NcApplication.Instance.IsBackgroundAbateRequired = false;
            NachoCore.Model.NcModel.Instance.RateLimiter.Enabled = false;
            // NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "RegularPriority sent Info_BackgroundAbateStopped from {0}", caller);
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStopped),
                Account = NachoCore.Model.ConstMcAccount.NotAccountSpecific,
                Stamp = DateTime.UtcNow,
            });
        }

        public static TimeSpan DeliveryTime (StatusIndEventArgs e)
        {
            var deliveryTime = DateTime.UtcNow - e.Stamp;
            return deliveryTime;
        }
    }
}

