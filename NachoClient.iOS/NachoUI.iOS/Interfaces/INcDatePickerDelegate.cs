//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoClient.iOS
{
    public interface INcDatePickerDelegate
    {
        void DismissDatePicker (DatePickerViewController vc, DateTime utcChosenDateTime);
    }
}

