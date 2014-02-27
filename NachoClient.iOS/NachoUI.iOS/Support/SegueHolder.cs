//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;

namespace NachoClient.iOS
{
    public class SegueHolder<T> : NSObject
    {
        public T value { get; private set; }

        /// <summary>
        /// Creates an NSObject to hold a typed C# object
        /// </summary>
        /// <param name="value">Value.</param>
        public SegueHolder (T value)
        {
            this.value = value;
        }
    }
}
