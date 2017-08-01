//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Foundation;
using CallKit;
using SQLite;
using NachoClient.Build;

namespace NachoClientCallerID.iOS
{
    [Register ("CallDirectoryHandler")]
    public class CallDirectoryHandler : CXCallDirectoryProvider, ICXCallDirectoryExtensionContextDelegate
    {

        struct Entry
        {
            public long PhoneNumber { get; private set; }
            public string Label { get; private set; }

            public Entry (long phoneNumber, string label)
            {
                PhoneNumber = phoneNumber;
                Label = label;
            }

            public static Entry? Create (string phoneString, string firstName, string lastName, string displayName, string companyName)
            {
                if (PhoneNumberFromString (phoneString, out var phoneNumber) && LabelFromNames (firstName, lastName, displayName, companyName, out var label)) {
                    return new Entry (phoneNumber, label);
                }
                return null;
            }

            static bool PhoneNumberFromString (string phoneString, out long number)
            {
                // iOS wants phone numbers represented as longs, so we need to strip all the non digit chars
                // and convert the result to a long.
                // iOS also wants the country code alway in front, so we'll first do a little detection to see
                // if it looks like there's a country code.  The current detection is very basic, assuming anything
                // with a + or a 1.  If this proves insufficient, we should probably go to a full phone number parsing library.
                phoneString = phoneString.Split (new char [] { ',' }) [0];
                if (!phoneString.StartsWith ("+", StringComparison.Ordinal) && !phoneString.StartsWith ("1", StringComparison.Ordinal)) {
                    phoneString = "+1" + phoneString;
                }
                var digits = Regex.Replace (phoneString, "\\D", "");
                if (!string.IsNullOrEmpty (digits)) {
                    if (long.TryParse (digits, out number)) {
                        return true;
                    }
                }
                number = 0;
                return false;
            }

            static bool LabelFromNames (string firstName, string lastName, string displayName, string companyName, out string label)
            {
                if (!string.IsNullOrWhiteSpace (displayName)) {
                    label = displayName.Trim ();
                    return true;
                }
                if (!string.IsNullOrWhiteSpace (firstName) && !string.IsNullOrWhiteSpace (lastName)) {
                    label = string.Format ("{0} {1}", firstName.Trim (), lastName.Trim ());
                    return true;
                }
                if (!string.IsNullOrWhiteSpace (firstName)) {
                    label = firstName.Trim ();
                    return true;
                }
                if (!string.IsNullOrWhiteSpace (lastName)) {
                    label = lastName.Trim ();
                    return true;
                }
                if (!string.IsNullOrWhiteSpace (companyName)) {
                    label = companyName.Trim ();
                    return true;
                }
                label = "";
                return false;
            }
        }

        protected CallDirectoryHandler (IntPtr handle) : base (handle)
        {
            // Note: this .ctor should not contain any initialization logic.
        }

        public override void BeginRequestWithExtensionContext (NSExtensionContext context)
        {

            var callContext = (CXCallDirectoryExtensionContext)context;
            callContext.Delegate = this;

            var entries = GetSortedEntries ();
            foreach (var entry in entries) {
                callContext.AddIdentificationEntry (entry.PhoneNumber, entry.Label);
            }

            callContext.CompleteRequest (null);
        }

        string GetDatabasePath ()
        {
            var container = NSFileManager.DefaultManager.GetContainerUrl (BuildInfo.AppGroup);
            if (container != null) {
                return container.Append ("Data", true).Append ("db", false).Path;
            }
            return null;
        }

        List<Entry> GetSortedEntries ()
        {
            // Using sqlite directly instead of NcModel to avoid including a bunch of unecessary code in this
            // extension's executable.  It's such a simple query we do, there's really no need for NcModel.
            // However, be aware that any changes to the model that affect these columns will require changes here.
            var entries = new List<Entry> ();

            var dbpath = GetDatabasePath ();
            if (dbpath == null) {
                return entries;
            }
            using (var conn = new SQLiteConnection (dbpath, storeDateTimeAsTicks: true)) {
                var statement = SQLite3.Prepare2 (conn.Handle, "SELECT a.Value, c.FirstName, c.LastName, c.DisplayName, c.CompanyName FROM McContactStringAttribute a JOIN McContact c ON a.ContactId = c.Id WHERE a.Type = 1 AND c.Source != 2");
                SQLite3.Result result;
                Entry? entry;
                do {
                    result = SQLite3.Step (statement);
                    entry = Entry.Create (
                        SQLite3.ColumnString (statement, 0),
                        SQLite3.ColumnString (statement, 1),
                        SQLite3.ColumnString (statement, 2),
                        SQLite3.ColumnString (statement, 3),
                        SQLite3.ColumnString (statement, 4)
                    );
                    if (entry.HasValue) {
                        entries.Add (entry.Value);
                    }
                } while (result == SQLite3.Result.Row);
                SQLite3.Finalize (statement);
            }

            // Entries must be sorted ascending according to Apple docs
            entries.Sort ((x, y) => {
                var diff = x.PhoneNumber - y.PhoneNumber;
                // since diff is a long, we can't just cast to in without the sign possibly changing,
                // so do an explicit check on sign of diff
                if (diff < 0) {
                    return -1;
                }
                if (diff > 0) {
                    return 1;
                }
                return 0;
            });
            return entries;
        }

        public void RequestFailed (CXCallDirectoryExtensionContext extensionContext, NSError error)
        {
            // An error occurred while adding blocking or identification entries, check the NSError for details.
            // For Call Directory error codes, see the CXErrorCodeCallDirectoryManagerError enum.
            //
            // This may be used to store the error details in a location accessible by the extension's containing app, so that the
            // app may be notified about errors which occured while loading data even if the request to load data was initiated by
            // the user in Settings instead of via the app itself.
        }
    }
}
