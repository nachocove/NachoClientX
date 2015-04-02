//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    /// <summary>
    /// Corresponds to UIAlertActionStyle
    /// </summary>
    public enum NcAlertActionStyle
    {
        Default,
        Cancel,
        Destructive,
    }

    /// <summary>
    /// A wrapper for UIAlertAction.  It is used with NcAlertView and NcActionSheet.  NcAlertAction is used on both
    /// iOS 7 and iOS 8, even though UIAlertAction is only available on iOS 8.
    /// </summary>
    public class NcAlertAction
    {
        public string Title;
        public Action Action;
        public NcAlertActionStyle Style;

        public NcAlertAction (string title, NcAlertActionStyle style, Action action)
        {
            Title = title;
            Action = action;
            Style = style;
        }

        public NcAlertAction (string title, Action action)
        {
            Title = title;
            Action = action;
            Style = NcAlertActionStyle.Default;
        }

        public UIAlertActionStyle UIStyle ()
        {
            switch (Style) {
            case NcAlertActionStyle.Default:
                return UIAlertActionStyle.Default;
            case NcAlertActionStyle.Destructive:
                return UIAlertActionStyle.Destructive;
            case NcAlertActionStyle.Cancel:
                return UIAlertActionStyle.Cancel;
            default:
                NcAssert.CaseError ("Unexpected AlertActionWrapperStyle");
                return UIAlertActionStyle.Default; // To keep the compiler happy.
            }
        }
    }
}

