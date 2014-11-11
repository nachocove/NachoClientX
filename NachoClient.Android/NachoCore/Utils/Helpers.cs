using System;
using NachoCore.Model;

namespace NachoCore.Utils
{
    // This is a grab-bag of extensions.
    public static class Uri_Helpers
    {
        public static bool IsHttps (this Uri uri)
        {
            return uri.Scheme.ToUpper () == "HTTPS";
        }

        public static bool IsValidHost (string host)
        {
            UriHostNameType hostnameURI = Uri.CheckHostName (host.Trim());
            if (hostnameURI == UriHostNameType.Dns || hostnameURI == UriHostNameType.IPv4 || hostnameURI == UriHostNameType.IPv6) {
                return true;
            }

            return false;
        }
    }

    public static class string_Helpers
    {
        public static string ToCapitalized (this string original)
        {
            if (string.IsNullOrEmpty (original)) {
                return original;
            }
            return char.ToUpper (original [0]) + original.Substring (1);
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

    public static class DateTime_Helpers
    {
        /// <summary>
        /// Convert DateTime to a local time, asserting our rule
        /// that times are stores in UTC or unspecified. Getting
        /// a local time here is a problem that must be fixed up
        /// stream.
        /// </summary>
        public static DateTime LocalT(this DateTime date)
        {
            switch(date.Kind) {
            case DateTimeKind.Utc:
                return date.ToLocalTime ();
            case DateTimeKind.Unspecified:
                return DateTime.SpecifyKind (date, DateTimeKind.Utc).ToLocalTime ();
            case DateTimeKind.Local:
                Log.Error (Log.LOG_UTILS, "LocalT received local time");
                return date;
            }
            NcAssert.CaseError ("C# compiler cannot do proper flow analysis.");
            return date;
        }
    }
}

