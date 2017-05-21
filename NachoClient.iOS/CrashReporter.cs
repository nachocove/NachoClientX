//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

#define HA_AUTH_ANONYMOUS
//#define HA_AUTH_USER
//#define HA_AUTH_EMAIL

using System;
using System.Diagnostics;
using System.IO;

using ObjCRuntime;
using Foundation;
using UIKit;

using NachoCore;
using NachoCore.Utils;
using NachoClient.Build;

#if HOCKEY_APP
using HockeyApp;
#endif

namespace NachoClient.iOS
{
    public class CrashReporter
    {
        public CrashReporter ()
        {
        }

        public void Start ()
        {
            if (Arch.SIMULATOR == Runtime.Arch) {
                // Xaramin does not produce .dSYM files. So, there is nothing to
                // upload to HockeyApp.
                //
                // For an explanation, see:
                // http://forums.xamarin.com/discussion/187/how-do-i-generate-dsym-for-simulator
                Log.Info (Log.LOG_LIFECYCLE, "Crash reporting is disabled on simulator");
                return;
            }

            if (Debugger.IsAttached) {
                Log.Info (Log.LOG_LIFECYCLE, "Crash reporting is disabled when debugger is attached");
                return;
            }

            #if HOCKEY_APP

            //We MUST wrap our setup in this block to wire up
            // Mono's SIGSEGV and SIGBUS signals
            HockeyApp.Setup.EnableCustomCrashReporting (() => {

                //Get the shared instance
                var manager = BITHockeyManager.SharedHockeyManager;

                //Configure it to use our APP_ID
                manager.Configure (BuildInfo.HockeyAppAppId, new HockeyAppCrashDelegate ());

                // Enable automatic reporting
                manager.CrashManager.CrashManagerStatus = BITCrashManagerStatus.AutoSend;
                manager.CrashManager.EnableOnDeviceSymbolication = false;
                if (BuildInfo.Version.StartsWith ("DEV")) {
                    manager.DebugLogEnabled = true;
                }

                //Start the manager
                manager.StartManager ();

                //Authenticate (there are other authentication options)
                #if HA_AUTH_ANONYMOUS
                manager.Authenticator.IdentificationType = BITAuthenticatorIdentificationType.Anonymous;
                #endif
                #if HA_AUTH_USER
                manager.Authenticator.IdentificationType = BITAuthenticatorIdentificationType.HockeyAppUser;
                manager.Authenticator.Delegate = new HockeyAppAuthenticatorDelegate ();
                #endif
                #if HA_AUTH_EMAIL
                manager.Authenticator.IdentificationType = BITAuthenticatorIdentificationType.HockeyAppEmail;
                manager.Authenticator.AuthenticationSecret = "fc041d7edcdd8b93951be3d4b9dd05d2";
                #endif
                manager.Authenticator.AuthenticateInstallation ();

                //Rethrow any unhandled .NET exceptions as native iOS
                // exceptions so the stack traces appear nicely in HockeyApp
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => {
                    try {
                        var ex = e.ExceptionObject as Exception;
                        if (null != ex) {
                            // See if we can get the part of the stack that is getting lost in ThrowExceptionAsNative().
                            Log.Error (Log.LOG_LIFECYCLE, "UnhandledException: {0}", ex);
                        }
                    } catch {
                    }
                    Setup.ThrowExceptionAsNative (e.ExceptionObject);
                };

                NcApplication.UnobservedTaskException += (sender, e) =>
                    Setup.ThrowExceptionAsNative (e.Exception);
            });
            #endif
        }

        public void SetCrashFolder ()
        {
            #if HOCKEY_APP
            if (null == NcApplication.Instance.CrashFolder) {
                var cacheFolder = NSSearchPath.GetDirectories (NSSearchPathDirectory.CachesDirectory, NSSearchPathDomain.User, true) [0];
                NcApplication.Instance.CrashFolder = Path.Combine (cacheFolder, "net.hockeyapp.sdk.ios");
                NcApplication.Instance.MarkStartup ();
            }
            #endif
        }

    }

    
    #if HOCKEY_APP

    public class HockeyAppCrashDelegate : BITCrashManagerDelegate
    {
        public HockeyAppCrashDelegate () : base ()
        {
        }

        public override string ApplicationLogForCrashManager (BITCrashManager crashManager)
        {
            return NcApplication.ApplicationLogForCrashManager ();
        }

        /// For some reason, UserName in HockeyApp web portal has a UUID prefixing the user name.
        /// On a narrow or normal browser window width, the user name is hidden. So, repeat it
        /// in contact again.
        private string UserName ()
        {
            string userName = null;
            if (BuildInfoHelper.IsDev) {
                userName = BuildInfo.User;
            }
            return userName;
        }

        public override string UserEmailForCrashManager (BITCrashManager crashManager)
        {
            return UserName ();
        }

        public override string UserNameForCrashManager (BITCrashManager crashManager)
        {
            return UserName ();
        }
    }

    public class HockeyAppAuthenticatorDelegate : BITAuthenticatorDelegate
    {
        public override void WillShowAuthenticationController (BITAuthenticator authenticator, UIViewController viewController)
        {
            this.BeginInvokeOnMainThread (() => {
                bool done = false;

                UIAlertView av = new UIAlertView ();
                av.Title = "Authentication Required";
                av.Message = "In order to run this Nacho Mail beta client, you must authenticate with HockeyApp. " +
                "Please enter your HockeyApp credential in the next screen.";
                av.AddButton ("Continue");
                av.Clicked += (sender, buttonArgs) => {
                    done = true;
                };
                av.Show ();
                while (!done) {
                    NSRunLoop.Current.RunUntil (NSDate.FromTimeIntervalSinceNow (0.5));
                }
            });
        }
    }
    #endif
}
