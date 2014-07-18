//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using SQLite;
using MimeKit;

namespace NachoCore.Model
{
    public class McEmailAddress : McAbstrObjectPerAcc
    {
        public McEmailAddress ()
        {
        }

        [Indexed, Unique]
        public string CanonicalEmailAddress { get; set; }

        public string DisplayEmailAddress { get; set; }

        [Indexed] // pre-computed for fast search
        public string DisplayFirstName { get; set; }

        [Indexed] // pre-computed for fast search
        public string DisplayLastName { get; set; }

        public string DisplayName { get; set; }

        [Indexed]
        public int Score { get; set; }

        public bool IsHot { get; set; }

        public bool IsVip { get; set; }

        public bool IsBlacklisted { get; set; }


    }
}

