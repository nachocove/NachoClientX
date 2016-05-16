using System;
using NachoCore.Model;
using NachoPlatform;
using NachoCore.ActiveSync;

namespace NachoCore.Utils
{
    // This is a grab-bag of extensions.
    public static class Uri_Helpers
    {
        public static bool IsHttps (this Uri uri)
        {
            // Scheme is converted to lower case
            return uri.Scheme == Uri.UriSchemeHttps;
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

    public static class Network_Helpers
    {
        public static bool HasNetworkConnection ()
        {
            if (NcCommStatus.Instance.Status != NetStatusStatusEnum.Up) {
                return false;
            } else {
                return true;
            }
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
    }

    public static class Folder_Helpers
    {
        public static string TypesToCommaDelimitedString (Xml.FolderHierarchy.TypeCode[] types)
        {
            string typesAsString = "(";
            string[] typesToStringArray = new string[types.Length];

            for (int i = 0; i < types.Length; i++) {
                typesToStringArray [i] = ((int)types [i]).ToString ();
            }

            typesAsString += string.Join (",", typesToStringArray) + ")";
            return typesAsString;
        }

        public static string FilterShortString (FolderFilterOptions filterSetting)
        {
            switch (filterSetting) {
            case FolderFilterOptions.All:
                return "All";
            case FolderFilterOptions.Hot:
                return "Hot";
            case FolderFilterOptions.Focused:
                return "Focused";
            case FolderFilterOptions.Unread:
                return "Unread";
            default:
                Log.Error (Log.LOG_UTILS, "Unexpected value for FolderFilterOptions: {0} ({1})", filterSetting.ToString (), (int)filterSetting);
                return "Unknown";
            }
        }

        public static string FilterString (FolderFilterOptions filterSetting)
        {
            switch (filterSetting) {
            case FolderFilterOptions.All:
                return "All messages";
            case FolderFilterOptions.Hot:
                return "Hot messages";
            case FolderFilterOptions.Focused:
                return "Focused messages";
            case FolderFilterOptions.Unread:
                return "Unread messages";
            default:
                Log.Error (Log.LOG_UTILS, "Unexpected value for FolderFilterOptions: {0} ({1})", filterSetting.ToString (), (int)filterSetting);
                return "Unknown set of messages";
            }
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

    public static class PortNumber_Helpers
    {
        public static string CheckPortValidity (string postString, string inOrOut)
        {
            int port;
            if (!int.TryParse (postString, out port)) {
                return string.Format ("Invalid {0} port number. It must be a number.", inOrOut);
            }
            if (!IsValidPort(port)) {
                return string.Format ("Invalid {0} port number. it must be > 0 and < 65536.", inOrOut);
            }
            return null;
        }

        public static bool IsValidPort (int port)
        {
            if (port <= 0 || port > 65535) {
                return false;
            } else {
                return true;
            }
        }
    }
}

