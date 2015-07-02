//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class NcEmailAnalyzer
    {
        private NcEmailAnalyzer SharedInstance_ { get; set; }
        public NcEmailAnalyzer SharedInstance {
            get {
                if (null == SharedInstance_) {
                    SharedInstance_ = new NcEmailAnalyzer ();
                }
                return SharedInstance_;
            }
        }

        public NcEmailAnalyzer ()
        {
        }

        public void AnalyzeSubject (McEmailMessage emailMessage)
        {
            NcAssert.True (emailMessage.ScoreVersion < 1); // must be before version 1
        }

        public void AnalyzeBody (McEmailMessage emailMessage)
        {
        }
    }
}

