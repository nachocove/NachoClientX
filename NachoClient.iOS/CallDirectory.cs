//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Text.RegularExpressions;

using Foundation;
using UIKit;

using SQLite;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;
using NachoClient.Build;

namespace NachoClient.iOS
{
    public class CallDirectory
    {

        public static CallDirectory Instance = new CallDirectory ();
        NSUserDefaults Defaults;

        private const string DefaultsKeyInitialUpdateRequested = "CallDirectoryInitialUpdateRequested";
        private const string DefaultsKeyRequestCount = "CallDirectoryRequestCount";
        private const string DefaultsKeyRequestFinished = "CallDirectoryRequestFinished";
        private const string DefaultsKeyRequestError = "CallDirectoryRequestError";
        private const string DefaultsKeyRequestExpired = "CallDirectoryRequestExpired";
        private const string DefaultsKeyEntryCount = "CallDirectoryEntryCount";
        private const string DefaultsKeyErrorCode = "CallDirectoryErrorCode";
        private const string DefaultsKeyErrorDomain = "CallDirectoryErrorDomain";
        private const string DefaultsKeyErrorDescription = "CallDirectoryErrorDescription";

        private CallDirectory ()
        {
            Defaults = new NSUserDefaults (BuildInfo.AppGroup, NSUserDefaultsType.SuiteName);
        }

        #region Lifecycle

        public void BecomeActive ()
        {
            StartListeningForStatusInd ();
        }

        public void BecomeInactive ()
        {
            StopListeningForStatusInd ();
        }

        #endregion

        #region Contacts

        bool IsUpdating;
        bool IsUpdateRequsted;

        public void RequestUpdate ()
        {
            if (!IsUpdating) {
                IsUpdating = true;
                NcTask.Run (Update, "CallDirectoryUpdate");
            } else {
                IsUpdateRequsted = true;
            }
        }

        void Update ()
        {
            Log.Info (Log.LOG_CONTACTS, "Updating CallDirectory");
            var mainDb = NcModel.Instance.Db;
            using (var sharedDb = GetSharedDb ()) {
                sharedDb?.RunInTransaction (() => {
                    var entryCount = 0;
                    sharedDb.Execute ("DROP TABLE IF EXISTS CallDirectory");
                    sharedDb.Execute ("CREATE TABLE CallDirectory (PhoneNumber INT, Label TEXT)");
                    sharedDb.Execute ("CREATE INDEX IX_PhoneNumber ON CallDirectory (PhoneNumber)");
                    var insertStatement = SQLite3.Prepare2 (sharedDb.Handle, "INSERT INTO CallDirectory (PhoneNumber, Label) VALUES (?, ?)");
                    var selectStatement = SQLite3.Prepare2 (mainDb.Handle, "SELECT DISTINCT a.Value, c.FirstName, c.LastName, c.DisplayName, c.CompanyName FROM McContactStringAttribute a JOIN McContact c ON a.ContactId = c.Id WHERE a.Type = ? AND c.Source != ?");
                    SQLite3.BindInt (selectStatement, 1, (int)McContactStringType.PhoneNumber);
                    SQLite3.BindInt (selectStatement, 2, (int)McAbstrItem.ItemSource.Device);
                    SQLite3.Result result;
                    do {
                        result = SQLite3.Step (selectStatement);
                        if (CreateEntry (
                            SQLite3.ColumnString (selectStatement, 0),
                            SQLite3.ColumnString (selectStatement, 1),
                            SQLite3.ColumnString (selectStatement, 2),
                            SQLite3.ColumnString (selectStatement, 3),
                            SQLite3.ColumnString (selectStatement, 4),
                            out var phoneNumber,
                            out var label
                        )) {
                            SQLite3.BindInt64 (insertStatement, 1, phoneNumber);
                            SQLite3.BindText (insertStatement, 2, label, -1, new IntPtr (-1));
                            SQLite3.Step (insertStatement);
                            SQLite3.Reset (insertStatement);
                            ++entryCount;
                        }
                    } while (result == SQLite3.Result.Row);
                    SQLite3.Finalize (insertStatement);
                    SQLite3.Finalize (selectStatement);
                    Log.LOG_CONTACTS.Info ("CallDirectory Update done with {0} entries", entryCount);
                });
                // Making a copy from the shared container to our local container allows easy access via Xcode container download
                // useful for debugging only
                //var copypath = Path.Combine (NcApplication.GetDocumentsPath (), "shareddbcopy");
                //if (File.Exists (copypath)) {
                //    File.Delete (copypath);
                //}
                //File.Copy (GetSharedDbPath (), copypath);
            }
            InvokeOnUIThread.Instance.Invoke (FinishUpdate);
        }

        void FinishUpdate ()
        {
            IsUpdating = false;
            if (IsUpdateRequsted) {
                IsUpdateRequsted = false;
                NcTask.Run (Update, "CallDirectoryUpdate");
            } else {
                NotifySystemOfUpdate ();
            }
        }

        void NotifySystemOfUpdate ()
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion (10, 0)) {
                CallKit.CXCallDirectoryManager.SharedInstance.GetEnabledStatusForExtension (BuildInfo.CallerIdBundleId, (status, statusError) => {
                    if (statusError != null) {
                        Log.LOG_CONTACTS.Warn ("Error checking extension status: {0}", statusError);
                    } else if (status != CallKit.CXCallDirectoryEnabledStatus.Disabled) {
                        Log.LOG_CONTACTS.Info ("Requesting Call Directory reload");
                        CallKit.CXCallDirectoryManager.SharedInstance.ReloadExtension (BuildInfo.CallerIdBundleId, (error) => {
                            if (error != null) {
                                Log.LOG_CONTACTS.Warn ("Error reloading call directory extension: {0}", error);
                            }
                            Log.LOG_CONTACTS.Info ("Call Directory reload done. #{4} finished={0}, expired={1}, error={2}, count={3}", Defaults.BoolForKey (DefaultsKeyRequestFinished), Defaults.BoolForKey (DefaultsKeyRequestExpired), Defaults.BoolForKey (DefaultsKeyRequestError), Defaults.IntForKey (DefaultsKeyEntryCount), Defaults.IntForKey (DefaultsKeyRequestCount));
                            if (Defaults.BoolForKey (DefaultsKeyRequestError)) {
                                Log.LOG_CONTACTS.Warn ("Call Directory error: {0} ({1}): {2}", Defaults.IntForKey (DefaultsKeyErrorCode), Defaults.StringForKey (DefaultsKeyErrorDomain), Defaults.StringForKey (DefaultsKeyErrorDescription));
                            }
                        });
                    } else {
                        Log.LOG_CONTACTS.Info ("Call Directory extension disabled");
                    }
                });
            }
        }

        SQLiteConnection GetSharedDb ()
        {
            var path = GetSharedDbPath ();
            if (path != null) {
                return new SQLiteConnection (path, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.NoMutex, storeDateTimeAsTicks: true);
            }
            return null;
        }

        string GetSharedDbPath ()
        {
            var container = NSFileManager.DefaultManager.GetContainerUrl (BuildInfo.AppGroup);
            if (container != null) {
                var path = container.Append ("Documents", true).Append ("Data", true).Append ("shareddb", false).Path;
                if (!Directory.Exists (Path.GetDirectoryName (path))) {
                    Directory.CreateDirectory (Path.GetDirectoryName (path));
                }
                return path;
            }
            return null;
        }

        static bool CreateEntry (string phoneString, string firstName, string lastName, string displayName, string companyName, out long phoneNumber, out string label)
        {
            phoneNumber = 0;
            label = "";
            return LabelFromNames (firstName, lastName, displayName, companyName, out label) && NumberFromString (phoneString, out phoneNumber);
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

        static bool NumberFromString (string phoneString, out long phoneNumber)
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
                if (long.TryParse (digits, out phoneNumber)) {
                    return true;
                }
            }
            phoneNumber = 0;
            return false;
        }

        #endregion

        #region System Events

        bool IsListeningForStatusInd;

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd) {
                if (!Defaults.BoolForKey (DefaultsKeyInitialUpdateRequested) && NcApplication.Instance.IsUp ()) {
                    Log.LOG_CONTACTS.Info ("CallDirectory needs initial update");
                    RequestUpdate ();
                    Defaults.SetBool (true, DefaultsKeyInitialUpdateRequested);
                }
                NcApplication.Instance.StatusIndEvent += StatusIndCallback;
                IsListeningForStatusInd = true;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndCallback;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndCallback (object obj, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;
            switch (statusEvent.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                if (!Defaults.BoolForKey (DefaultsKeyInitialUpdateRequested) && NcApplication.Instance.IsUp ()) {
                    Log.LOG_CONTACTS.Info ("CallDirectory needs initial update");
                    RequestUpdate ();
                    Defaults.SetBool (true, DefaultsKeyInitialUpdateRequested);
                }
                break;
            case NcResult.SubKindEnum.Info_ContactChanged:
                Log.LOG_CONTACTS.Info ("CallDirectory needs update becaue contact changed");
                RequestUpdate ();
                break;
            case NcResult.SubKindEnum.Info_ContactSetChanged:
                Log.LOG_CONTACTS.Info ("CallDirectory needs update becaue contact set changed");
                RequestUpdate ();
                break;
            }
        }

        #endregion
    }
}
