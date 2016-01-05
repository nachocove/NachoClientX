//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using System.IO;
using NachoClient.Build;

using Foundation;
using UIKit;

namespace NachoClientShare.iOS
{
    public partial class ShareViewController : UIViewController
    {
        string StashName;
        NSUrl StashUrl;
        int RemainingAttachments = 0;

        public ShareViewController (IntPtr handle) : base (handle)
        {
        }

        public override void DidReceiveMemoryWarning ()
        {
            // Releases the view if it doesn't have a superview.
            base.DidReceiveMemoryWarning ();

            // Release any cached data, images, etc that aren't in use.
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Do any additional setup after loading the view.
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            StashFiles ();
        }

        void Complete (bool openApp = true)
        {
            if (openApp) {
                OpenApp ();
            }
            ExtensionContext.CompleteRequest (null, null);
        }

        void StashFiles ()
        {
            var uuid = new NSUuid ();
            StashName = uuid.AsString ();
            var containerUrl = NSFileManager.DefaultManager.GetContainerUrl (BuildInfo.AppGroup);
            if (containerUrl != null) {
                StashUrl = containerUrl.Append (StashName, true);
                Directory.CreateDirectory (StashUrl.Path);
                RemainingAttachments = 0;
                foreach (var item in ExtensionContext.InputItems) {
                    foreach (var attachment in item.Attachments) {
                        ++RemainingAttachments;
                    }
                }
                if (RemainingAttachments > 0) {
                    foreach (var item in ExtensionContext.InputItems) {
                        foreach (var attachment in item.Attachments) {
                            attachment.LoadItem ("public.data", null, HandleAttachmentLoad);
                        }
                    }
                } else {
                    Console.WriteLine ("NACHO ERROR: Nothing to share");
                    Complete (false);
                }
            } else {
                Console.WriteLine ("NACHO ERROR: No container URL");
                Complete (false);
            }
        }

        void HandleAttachmentLoad (NSObject provider, NSError error)
        {
            --RemainingAttachments;
            if (error == null) {
                var url = provider as NSUrl;
                if (url != null) {
                    var filename = url.LastPathComponent;
                    var filenameRoot = Path.GetFileNameWithoutExtension (filename);
                    var ext = Path.GetExtension (filename);
                    var destinationPath = StashUrl.Append (filename, false).Path;
                    var counter = 1;
                    while (File.Exists (destinationPath)) {
                        filename = String.Format ("{0}-{1}{2}", filenameRoot, counter, ext);
                        destinationPath = StashUrl.Append (filename, false).Path;
                        ++counter;
                    }
                    if (url.IsFileUrl) {
                        File.Copy (url.Path, destinationPath);
                    } else {
                        Console.WriteLine ("NACHO ERROR: provider is not a file NSUrl");
                    }
                } else {
                    Console.WriteLine ("NACHO ERROR: provider is not an NSUrl");
                }
            } else {
                Console.WriteLine ("NACHO ERROR: Error loading attachment: {0} {1}", error.Code, error.Description);
            }
            if (RemainingAttachments == 0) {
                this.BeginInvokeOnMainThread (() => {
                    Complete ();
                });
            }
        }

        void OpenApp ()
        {
            UIResponder responder = this;
            var openURL = new ObjCRuntime.Selector ("openURL:");
            while (responder != null && !responder.RespondsToSelector (openURL)) {
                responder = responder.NextResponder;
            }
            if (responder != null) {
                var nachoSchemeObject = NSBundle.MainBundle.InfoDictionary.ObjectForKey (new NSString ("CFBundleIdentifier"));
                var nachoScheme = nachoSchemeObject.ToString ();
                // The scheme is the same as our parent app's bundle ID, which this extension's bundle ID minus the final .whatever
                nachoScheme = nachoScheme.Substring (0, nachoScheme.LastIndexOf ('.'));
                var url = new NSUrl (String.Format ("{0}:///share/{1}", nachoScheme, StashName));
                responder.PerformSelector (openURL, url);
            }
        }
    }
}

