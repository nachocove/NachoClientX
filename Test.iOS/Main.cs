// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using UIKit;

namespace Test.iOS
{
    public class Application
    {
        // This is the main entry point of the application.
        static void Main (string[] args)
        {
            // if you want to use a different Application Delegate class from "UnitTestAppDelegate"
            // you can specify it here.
            try {
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
                    Console.WriteLine ("Unhandle exception: {0}", e.ExceptionObject);
                    throw e.ExceptionObject as Exception;
                };
                UIApplication.Main (args, null, "UnitTestAppDelegate");
            } catch (Exception e) {
                Console.WriteLine ("Uncaught exception: {0}", e);
                throw;
            }
        }
    }
}
