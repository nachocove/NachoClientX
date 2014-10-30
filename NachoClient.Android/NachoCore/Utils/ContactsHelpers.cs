
using System;
using System.Collections;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class ContactsHelper
    {
        public ContactsHelper ()
        {
        }

        public static string GetInitials (McContact contact)
        {
            string initials = "";
            if (!String.IsNullOrEmpty (contact.FirstName)) {
                initials += Char.ToUpper (contact.FirstName [0]);
            }
            if (!String.IsNullOrEmpty (contact.LastName)) {
                initials += Char.ToUpper (contact.LastName [0]);
            }
            // Or, failing that, the first char
            if (String.IsNullOrEmpty (initials)) {
                if (0 != contact.EmailAddresses.Count) {
                    var emailAddressAttribute = contact.EmailAddresses [0];
                    var emailAddress = McEmailAddress.QueryById<McEmailAddress> (emailAddressAttribute.EmailAddress);
                    foreach (char c in emailAddress.CanonicalEmailAddress) {
                        if (Char.IsLetterOrDigit (c)) {
                            initials += Char.ToUpper (c);
                            break;
                        }
                    }
                }
            }
            return initials;
        }
    }
}





