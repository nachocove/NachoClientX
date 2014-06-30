﻿using System;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

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

        void AddressBlockAddContactClicked(UcAddressBlock view, string prefix);
    }
}

