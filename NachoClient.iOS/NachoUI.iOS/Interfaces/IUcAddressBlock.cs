using System;
using CoreGraphics;
using System.Collections.Generic;

using Foundation;
using UIKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public interface IUcAddressBlock
    {
    }

    public interface IUcAddressBlockDelegate
    {
        void AddressBlockNeedsLayout (UcAddressBlock view);

        void AddressBlockWillBecomeActive (UcAddressBlock view);

        void AddressBlockWillBecomeInactive (UcAddressBlock view);

        void AddressBlockAutoCompleteContactClicked(UcAddressBlock view, string prefix);

        void AddressBlockContactPickerRequested(UcAddressBlock view);

        void AddressBlockRemovedAddress (UcAddressBlock view, NcEmailAddress address);
    }
}

