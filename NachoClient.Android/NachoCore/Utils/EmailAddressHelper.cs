//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Net.Mail;

namespace NachoCore.Utils
{
    public class EmailAddressHelper
    {
        public static List<MailAddress> ParseString (string emailAddressString)
        {
            List<MailAddress> emailAddressList = new List<MailAddress> ();
            char[] comma = new char[] {','};
            string[] emailAddressStrings = 
                emailAddressString.Split (comma, 1000000, StringSplitOptions.RemoveEmptyEntries); // TODO - hardcoded limit.
            foreach (string s in emailAddressStrings) {
                MailAddress emailAddress = new MailAddress (s.Trim ().Replace("\"", ""));
                emailAddressList.Add (emailAddress);
            }
            return emailAddressList;
        }
    }
}

