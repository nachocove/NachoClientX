// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using UIKit;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore;

namespace NachoClient.iOS
{
    public class Application
    {
        // This is the main entry point of the application.
        static void Main (string[] args)
        {
            // if you want to use a different Application Delegate class from "AppDelegate"
            // you can specify it here.
            try {
                UIApplication.Main (args, null, "AppDelegate");
            } catch (Exception ex) {
                // Look for XAMMITs. We can't recover here, but we can know via telemetry rather than crashdump.
                if (ex is System.IO.IOException && ex.Message.Contains ("Tls.RecordProtocol.BeginSendRecord")) {
                    Log.Error (Log.LOG_SYS, "XAMMIT AggregateException: IOException with Tls.RecordProtocol.BeginSendRecord");
                } else {
                    if (ex is System.IO.IOException && ex.Message.Contains ("Too many open files")) {
                        Log.Error (Log.LOG_SYS, "Main:{0}: Dumping File Descriptors", ex.Message);
                        NcApplicationMonitor.DumpFileLeaks ();
                        NcApplicationMonitor.DumpFileDescriptors ();
                        NcModel.Instance.DumpLastAccess ();
                    }
                    throw;
                }
            }
        }
    }
}
