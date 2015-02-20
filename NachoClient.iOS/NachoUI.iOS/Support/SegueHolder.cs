//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;

namespace NachoClient.iOS
{
    // Warning MT4112: The registrar found a generic type: NachoClient.iOS.SegueHolder`1.
    // Registering generic types with ObjectiveC is not supported, and will lead to random behavior and/or crashes. (MT4112)
    public class SegueHolder : NSObject
    {
        public object value { get; private set; }
        public object value2 { get; private set; }

        /// <summary>
        /// Creates an NSObject to hold a typed C# object
        /// </summary>
        /// <param name="value">Value.</param>
        public SegueHolder (Object value)
        {
            this.value = value;
        }

        public SegueHolder (Object value, Object value2)
        {
            this.value = value;
            this.value2 = value2;
        }
    }
}
