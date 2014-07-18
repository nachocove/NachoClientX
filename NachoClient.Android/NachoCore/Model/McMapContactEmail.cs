//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;

namespace NachoCore.Model
{
    public class McMapContactEmail : McAbstrObjectPerAcc
    {
        public McMapContactEmail ()
        {

        }

        // Contact sources
        public enum ContactSource
        {
            EAS = 1,
            GAL = 2,
            RIC = 3,
            Gleaner = 4,
            Device = 10,
        };

        // Source of contact, affects the interpretation of ContactIndex
        public ContactSource Source { get; set; }


        [Indexed] // pointer to the McEmailAddress
        public int EmailAddressIndex { get; set; }

        [Indexed] // contact-type specific point; a McContact index for EAS, GAL, and Gleaned contacts
        public int ContactIndex { get; set; }
    }
}

