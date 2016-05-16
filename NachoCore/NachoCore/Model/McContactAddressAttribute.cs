//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using SQLite;
using System;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    /// <summary>
    /// Address attributes, like business or home address
    /// </summary>
    public class McContactAddressAttribute : McAbstrContactAttribute
    {
        /// Street address of the contact's alternate address
        public string Street { get; set; }

        /// City for the contact's alternate address
        public string City { get; set; }

        /// State of the contact's alternate address
        public string State { get; set; }

        /// Country/region of the contact's alternate address
        public string Country { get; set; }

        /// Postal code of the contact's alternate address
        public string PostalCode { get; set; }
    }
}

