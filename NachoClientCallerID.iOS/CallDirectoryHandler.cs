//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;

using Foundation;
using CallKit;
using SQLite;
using NachoClient.Build;

namespace NachoClientCallerID.iOS
{
    [Register ("CallDirectoryHandler")]
    public class CallDirectoryHandler : CXCallDirectoryProvider, ICXCallDirectoryExtensionContextDelegate
    {

        NSUserDefaults Defaults;

        private const string DefaultsKeyRequestCount = "CallDirectoryRequestCount";
        private const string DefaultsKeyRequestFinished = "CallDirectoryRequestFinished";
        private const string DefaultsKeyRequestError = "CallDirectoryRequestError";
        private const string DefaultsKeyRequestExpired = "CallDirectoryRequestExpired";
        private const string DefaultsKeyEntryCount = "CallDirectoryEntryCount";
        private const string DefaultsKeyErrorCode = "CallDirectoryErrorCode";
        private const string DefaultsKeyErrorDomain = "CallDirectoryErrorDomain";
        private const string DefaultsKeyErrorDescription = "CallDirectoryErrorDescription";

        protected CallDirectoryHandler (IntPtr handle) : base (handle)
        {
            // Note: this .ctor should not contain any initialization logic.
        }

        public override void BeginRequestWithExtensionContext (NSExtensionContext context)
        {
            var callContext = (CXCallDirectoryExtensionContext)context;
            callContext.Delegate = this;
            Defaults = new NSUserDefaults (NachoClient.Build.BuildInfo.AppGroup, NSUserDefaultsType.SuiteName);
            Defaults.SetInt (Defaults.IntForKey (DefaultsKeyRequestCount) + 1, DefaultsKeyRequestCount);
            Defaults.Synchronize ();
            Defaults.SetBool (false, DefaultsKeyRequestFinished);
            Defaults.SetBool (false, DefaultsKeyRequestError);
            Defaults.SetBool (false, DefaultsKeyRequestExpired);
            Defaults.SetInt (0, DefaultsKeyEntryCount);
            Defaults.SetInt (0, DefaultsKeyErrorCode);
            Defaults.SetString ("", DefaultsKeyErrorDomain);
            Defaults.SetString ("", DefaultsKeyErrorDescription);
            Defaults.Synchronize ();

            int entryCount = 0;

            try {

                using (var connection = GetDatabaseConnection ()) {
                    if (connection != null) {
                        var statement = SQLite3.Prepare2 (connection.Handle, "SELECT PhoneNumber, Label FROM CallDirectory ORDER BY PhoneNumber ASC");
                        SQLite3.Result result;
                        do {
                            result = SQLite3.Step (statement);
                            if (result == SQLite3.Result.Row) {
                                callContext.AddIdentificationEntry (SQLite3.ColumnInt64 (statement, 0), SQLite3.ColumnString (statement, 1));
                                ++entryCount;
                            }
                        } while (result == SQLite3.Result.Row);
                        SQLite3.Finalize (statement);
                    }
                }
            } catch (Exception e) {
                Defaults.SetBool (true, DefaultsKeyRequestError);
                Defaults.SetString ("Exception", DefaultsKeyErrorDomain);
                Defaults.SetString (e.ToString (), DefaultsKeyErrorDescription);
            }

            Defaults.SetInt (entryCount, DefaultsKeyEntryCount);
            Defaults.Synchronize ();

            callContext.CompleteRequest ((expired) => {
                if (expired) {
                    Defaults.SetBool (true, DefaultsKeyRequestExpired);
                } else {
                    Defaults.SetBool (true, DefaultsKeyRequestFinished);
                }
                Defaults.Synchronize ();
            });
        }

        SQLiteConnection GetDatabaseConnection ()
        {
            var path = GetDatabasePath ();
            if (File.Exists (path)) {
                return new SQLiteConnection (path, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.NoMutex, storeDateTimeAsTicks: true);
            }
            return null;
        }

        string GetDatabasePath ()
        {
            var container = NSFileManager.DefaultManager.GetContainerUrl (BuildInfo.AppGroup);
            return container.Append ("Documents", true).Append ("Data", true).Append ("shareddb", false).Path;
        }

        public void RequestFailed (CXCallDirectoryExtensionContext extensionContext, NSError error)
        {
            // An error occurred while adding blocking or identification entries, check the NSError for details.
            // For Call Directory error codes, see the CXErrorCodeCallDirectoryManagerError enum.
            //
            // This may be used to store the error details in a location accessible by the extension's containing app, so that the
            // app may be notified about errors which occured while loading data even if the request to load data was initiated by
            // the user in Settings instead of via the app itself.
            Defaults.SetBool (true, DefaultsKeyRequestError);
            Defaults.SetString (error.Domain, DefaultsKeyErrorDomain);
            Defaults.SetInt (error.Code, DefaultsKeyErrorCode);
            Defaults.SetString (error.Description, DefaultsKeyErrorDescription);
            Defaults.Synchronize ();
        }
    }
}
