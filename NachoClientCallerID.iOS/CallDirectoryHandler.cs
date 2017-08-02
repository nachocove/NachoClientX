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

        protected CallDirectoryHandler (IntPtr handle) : base (handle)
        {
            // Note: this .ctor should not contain any initialization logic.
        }

        public override void BeginRequestWithExtensionContext (NSExtensionContext context)
        {

            var callContext = (CXCallDirectoryExtensionContext)context;
            callContext.Delegate = this;

            using (var connection = GetDatabaseConnection ()) {
                if (connection != null) {
                    var statement = SQLite3.Prepare2 (connection.Handle, "SELECT PhoneNumber, Label FROM CallDirectory ORDER BY PhoneNumber ASC");
                    SQLite3.Result result;
                    do {
                        result = SQLite3.Step (statement);
                        callContext.AddIdentificationEntry (SQLite3.ColumnInt64 (statement, 0), SQLite3.ColumnString (statement, 1));
                    } while (result == SQLite3.Result.Row);
                    SQLite3.Finalize (statement);
                }
            }

            callContext.CompleteRequest (null);
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
        }
    }
}
