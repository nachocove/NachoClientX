//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//

// Copyright 2010 Miguel de Icaza
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using CoreGraphics;
using CoreLocation;
using CoreText;
using Foundation;
using NachoClient.iOS;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using UIKit;

namespace NachoClient
{
    public static class Util
    {
        /// <summary>
        ///   A shortcut to the main application
        /// </summary>
        public static UIApplication MainApp = UIApplication.SharedApplication;
        public readonly static string BaseDir = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), "..");
        //
        // Since we are a multithreaded application and we could have many
        // different outgoing network connections (api.twitter, images,
        // searches) we need a centralized API to keep the network visibility
        // indicator state
        //
        static object networkLock = new object ();
        static int active;

        public static void PushNetworkActive ()
        {
            lock (networkLock) {
                active++;
                MainApp.NetworkActivityIndicatorVisible = true;
            }
        }

        public static void PopNetworkActive ()
        {
            lock (networkLock) {
                active--;
                if (active == 0)
                    MainApp.NetworkActivityIndicatorVisible = false;
            }
        }

        public static DateTime LastUpdate (string key)
        {
            var s = Defaults.StringForKey (key);
            if (s == null)
                return DateTime.MinValue;
            long ticks;
            if (Int64.TryParse (s, out ticks))
                return new DateTime (ticks, DateTimeKind.Utc);
            else
                return DateTime.MinValue;
        }

        public static bool NeedsUpdate (string key, TimeSpan timeout)
        {
            return DateTime.UtcNow - LastUpdate (key) > timeout;
        }

        public static void RecordUpdate (string key)
        {
            Defaults.SetString (key, DateTime.UtcNow.Ticks.ToString ());
        }

        public static NSUserDefaults Defaults = NSUserDefaults.StandardUserDefaults;
        const long TicksOneDay = 864000000000;
        const long TicksOneHour = 36000000000;
        const long TicksMinute = 600000000;
        static string s1 = Locale.GetText ("1 sec");
        static string sn = Locale.GetText (" secs");
        static string m1 = Locale.GetText ("1 min");
        static string mn = Locale.GetText (" mins");
        static string h1 = Locale.GetText ("1 hour");
        static string hn = Locale.GetText (" hours");
        static string d1 = Locale.GetText ("1 day");
        static string dn = Locale.GetText (" days");

        public static string FormatTime (TimeSpan ts)
        {
            int v;

            if (ts.Ticks < TicksMinute) {
                v = ts.Seconds;
                if (v <= 1)
                    return s1;
                else
                    return v + sn;
            } else if (ts.Ticks < TicksOneHour) {
                v = ts.Minutes;
                if (v == 1)
                    return m1;
                else
                    return v + mn;
            } else if (ts.Ticks < TicksOneDay) {
                v = ts.Hours;
                if (v == 1)
                    return h1;
                else
                    return v + hn;
            } else {
                v = ts.Days;
                if (v == 1)
                    return d1;
                else
                    return v + dn;
            }
        }

        public static string StripHtml (string str)
        {
            if (str.IndexOf ('<') == -1)
                return str;
            var sb = new StringBuilder ();
            for (int i = 0; i < str.Length; i++) {
                char c = str [i];
                if (c != '<') {
                    sb.Append (c);
                    continue;
                }

                for (i++; i < str.Length; i++) {
                    c = str [i];
                    if (c == '"' || c == '\'') {
                        var last = c;
                        for (i++; i < str.Length; i++) {
                            c = str [i];
                            if (c == last)
                                break;
                            if (c == '\\')
                                i++;
                        }
                    } else if (c == '>')
                        break;
                }
            }
            return sb.ToString ();
        }

        public static string CleanName (string name)
        {
            if (name.Length == 0)
                return "";

            bool clean = true;
            foreach (char c in name) {
                if (Char.IsLetterOrDigit (c) || c == '_')
                    continue;
                clean = false;
                break;
            }
            if (clean)
                return name;

            var sb = new StringBuilder ();
            foreach (char c in name) {
                if (!Char.IsLetterOrDigit (c))
                    break;

                sb.Append (c);
            }
            return sb.ToString ();
        }


        static long lastTime;

        [Conditional ("TRACE")]
        public static void ReportTime (string s)
        {
            long now = DateTime.UtcNow.Ticks;

            Debug.WriteLine (string.Format ("[{0}] ticks since last invoke: {1}", s, now - lastTime));
            lastTime = now;
        }

        [Conditional ("TRACE")]
        public static void Log (string format, params object[] args)
        {
            Debug.WriteLine (String.Format (format, args));
        }

        public static void LogException (string text, Exception e)
        {
            using (var s = System.IO.File.AppendText (Util.BaseDir + "/Documents/crash.log")) {
                var msg = String.Format ("On {0}, message: {1}\nException:\n{2}", DateTime.Now, text, e.ToString ());
                s.WriteLine (msg);
                NachoCore.Utils.Log.Error (NachoCore.Utils.Log.LOG_UI, msg);
            }
        }

        static CultureInfo americanCulture;

        public static CultureInfo AmericanCulture {
            get {
                if (americanCulture == null)
                    americanCulture = new CultureInfo ("en-US");
                return americanCulture;
            }
        }

        #region Location

        internal class MyCLLocationManagerDelegate : CLLocationManagerDelegate
        {
            Action<CLLocation> callback;

            public MyCLLocationManagerDelegate (Action<CLLocation> callback)
            {
                this.callback = callback;
            }
            //            public override void UpdatedLocation (CLLocationManager manager, CLLocation newLocation, CLLocation oldLocation)
            //            {
            //                manager.StopUpdatingLocation ();
            //                locationManager = null;
            //                callback (newLocation);
            //            }
            public override void Failed (CLLocationManager manager, NSError error)
            {
                callback (null);
            }
        }

        static CLLocationManager locationManager;

        static public void RequestLocation (Action<CLLocation> callback)
        {
            locationManager = new CLLocationManager () {
                DesiredAccuracy = CLLocation.AccuracyBest,
                Delegate = new MyCLLocationManagerDelegate (callback),
                DistanceFilter = 1000f
            };
            if (CLLocationManager.LocationServicesEnabled)
                locationManager.StartUpdatingLocation ();
        }

        #endregion

        #region NachoCove

        public static List<UIColor> colors = new List<UIColor> () {
            UIColor.Clear,
            UIColor.LightGray,
            UIColor.FromRGB (0xFC, 0xC8, 0xC7),
            UIColor.FromRGB (0xF3, 0xB2, 0xB0),
            UIColor.FromRGB (0xF0, 0x91, 0x80),
            UIColor.FromRGB (0xEE, 0x70, 0x5B),
            UIColor.FromRGB (0xE6, 0x59, 0x59),
            UIColor.FromRGB (0xD2, 0x47, 0x47),
            UIColor.FromRGB (0x66, 0xAF, 0xA7),
            UIColor.FromRGB (0x2B, 0xD9, 0xB2),
            UIColor.FromRGB (0x00, 0x96, 0x88),
            UIColor.FromRGB (0x02, 0x82, 0x76),
            UIColor.FromRGB (0x01, 0x6B, 0x5E),
            UIColor.FromRGB (0x7A, 0xE3, 0xD8),
            UIColor.FromRGB (0x3B, 0xCE, 0xD9),
            UIColor.FromRGB (0x01, 0xB2, 0xCD),
            UIColor.FromRGB (0x00, 0x88, 0x95),
            UIColor.FromRGB (0x34, 0x7C, 0x9A),
            UIColor.FromRGB (0x00, 0x5F, 0x6F),
            UIColor.FromRGB (0xC5, 0xE1, 0xA5),
            UIColor.FromRGB (0xBE, 0xCA, 0x39),
            UIColor.FromRGB (0x7B, 0xB3, 0x3A),
            UIColor.FromRGB (0x3C, 0xB9, 0x6A),
            UIColor.FromRGB (0xA0, 0xAB, 0xB0),
            UIColor.FromRGB (0x70, 0x87, 0x92),
            UIColor.FromRGB (0x57, 0x75, 0x84),
            UIColor.FromRGB (0x4F, 0x64, 0x6D),
            UIColor.FromRGB (0xFF, 0xE1, 0x86),
            UIColor.FromRGB (0xFF, 0xD4, 0x56),
            UIColor.FromRGB (0xFA, 0xBF, 0x20),
            UIColor.FromRGB (0xF5, 0x98, 0x27),
            UIColor.FromRGB (0xEF, 0x7C, 0x00),
            UIColor.FromRGB (0xF3, 0x68, 0x00),
        };

        static Random random = new Random ();

        public static List<UIColor> accountColors = null;

        public static int PickRandomColorForUser ()
        {
            int randomNumber = random.Next (2, colors.Count);
            return randomNumber;
        }

        public static UIColor ColorForUser (int index)
        {
            if (0 > index) {
                NachoCore.Utils.Log.Warn (NachoCore.Utils.Log.LOG_UI, "ColorForUser not set");
                index = 1;
            }
            return colors [index];
        }

        static Dictionary<int, int> AccountColorIndexCache = new Dictionary<int, int> ();

        public static UIColor ColorForAccount (int accountId)
        {
            if (accountColors == null) {
                accountColors = new List<UIColor> (McAccount.AccountColors.Length / 3);
                for (int i = 0; i < McAccount.AccountColors.Length / 3; ++i) {
                    accountColors.Add (UIColor.FromRGB(McAccount.AccountColors [i,0], McAccount.AccountColors [i,1], McAccount.AccountColors [i,2]));
                }
            }
            if (!AccountColorIndexCache.ContainsKey (accountId)) {
                var account = McAccount.QueryById<McAccount> (accountId);
                AccountColorIndexCache [accountId] = account.ColorIndex;
            }
            var index = AccountColorIndexCache [accountId];
            return accountColors [index];
        }

        public static UIColor GetContactColor (McContact contact)
        {
            if (0 == contact.CircleColor) {
                contact.CircleColor = PickRandomColorForUser ();
            }
            return ColorForUser (contact.CircleColor);
        }


        public static NachoTabBarController GetActiveTabBarOrNull ()
        {
            var appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;

            NachoTabBarController activeTabBar;
            if (appDelegate.Window.RootViewController is NachoTabBarController) {
                activeTabBar = (NachoTabBarController)appDelegate.Window.RootViewController;
            } else if (null == appDelegate.Window.RootViewController.PresentedViewController) {
                return null;
            } else if (null != appDelegate.Window.RootViewController.PresentedViewController.TabBarController) {
                activeTabBar = (NachoTabBarController)appDelegate.Window.RootViewController.PresentedViewController.TabBarController;
            } else {
                activeTabBar = (NachoTabBarController)appDelegate.Window.RootViewController.PresentedViewController;
            }
            return activeTabBar;
        }

        public static NachoTabBarController GetActiveTabBar ()
        {
            var activeTabBar = GetActiveTabBarOrNull ();
            NcAssert.NotNull (activeTabBar);
            return activeTabBar;
        }

        public static UIImage DotWithColor (UIColor color)
        {
            UIGraphics.BeginImageContext (new CGSize (22, 22));
            var ctx = UIGraphics.GetCurrentContext ();

            ctx.SetFillColor (color.CGColor);
            ctx.FillEllipseInRect (new CGRect (5, 5, 12, 12));

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;
        }

        /// <summary>
        /// Colors to use to identify calendar folders.  These are temporary.  The final set of colors
        /// has not been decided yet.
        /// </summary>
        private static UIColor[] calendarColors = {
            UIColor.Blue,
            UIColor.Red,
            UIColor.Green,
            UIColor.Orange,
            UIColor.Purple,
            UIColor.Brown,
            UIColor.Gray,
            UIColor.Black,
        };

        public static UIColor CalendarColor (int colorIndex)
        {
            if (0 == colorIndex) {
                return UIColor.White;
            }
            return calendarColors [(colorIndex - 1) % calendarColors.Length];
        }

        public static UIImage DrawCalDot (UIColor circleColor, CGSize size)
        {
            var origin = new CGPoint (0, 0);

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var ctx = UIGraphics.GetCurrentContext ();

            ctx.SetFillColor (circleColor.CGColor);
            ctx.FillEllipseInRect (new CGRect (origin, size));

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;
        }

        public static UIImage DrawButtonBackgroundImage (UIColor color, CGSize size)
        {
            var origin = new CGPoint (0, 0);

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var ctx = UIGraphics.GetCurrentContext ();

            ctx.SetFillColor (color.CGColor);
            ctx.FillRect (new CGRect (origin, size));

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;
        }

        public static UIImage DrawTodayButtonImage (string day)
        {
            var size = new CGSize (24, 24);
            var origin = new CGPoint (0, 0);

            var todayImage = UIImage.FromBundle ("calendar-empty-cal-alt");

            var attributedString = new NSAttributedString (day,
                                       new UIStringAttributes {
                    Font = A.Font_AvenirNextMedium12
                });

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var ctx = UIGraphics.GetCurrentContext ();
            ctx.TranslateCTM (0, todayImage.Size.Height);
            ctx.ScaleCTM (1, -1);
            ctx.DrawImage (new CGRect (origin, size), todayImage.CGImage);

            ctx.TranslateCTM ((todayImage.Size.Width / 2) - (attributedString.Size.Width / 2), (todayImage.Size.Height / 2) - (attributedString.Size.Height / 2) + 5);
            using (var textLine = new CTLine (attributedString)) {
                textLine.Draw (ctx);
            }

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;
        }

        /// <summary>
        /// Takes a screenshot of the view passed in and returns an image
        /// </summary>
        public static UIImage captureView (UIView view)
        {
            UIGraphics.BeginImageContextWithOptions (view.Bounds.Size, false, 0.0f);
            view.Layer.RenderInContext (UIGraphics.GetCurrentContext ());
            var capturedImage = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return capturedImage;
        }

        public static void UserMessageField (string from, int accountId, out int ColorIndex, out string Initials)
        {
            // Parse the from address
            var mailboxAddress = NcEmailAddress.ParseMailboxAddressString (from);
            if (null == mailboxAddress) {
                ColorIndex = 1;
                Initials = "";
                return;
            }
            // And get a McEmailAddress
            McEmailAddress emailAddress;
            if (!McEmailAddress.Get (accountId, mailboxAddress, out emailAddress)) {
                ColorIndex = 1;
                Initials = "";
                return;
            }
            // Cache the color
            ColorIndex = emailAddress.ColorIndex;
            Initials = EmailHelper.Initials (from);
        }

        public static UIImage ContactToPortraitImage (McContact contact)
        {
            if (null == contact) {
                return null;
            }
            if (0 == contact.PortraitId) {
                return null;
            }
            return PortraitToImage (contact.PortraitId);
        }

        public static UIImage PortraitToImage (int portraitId)
        {
            if (0 == portraitId) {
                return null;
            }
            var data = McPortrait.GetContentsByteArray (portraitId);
            if (null == data) {
                return null;
            }
            return UIImage.LoadFromData (NSData.FromArray (data));
        }

        public static UIImage ImageOfSender (int accountId, string emailAddress)
        {
            List<McContact> contacts = McContact.QueryByEmailAddress (accountId, emailAddress);
            if ((null == contacts) || (0 == contacts.Count)) {
                return null;
            }
            // There may be more than one contact that matches an email address.
            // Search thru all of them and look for the first one that has a portrait.
            foreach (var contact in contacts) {
                UIImage image = ContactToPortraitImage (contact);
                if (null != image) {
                    return image;
                }
            }
            return null;
        }

        public static UIImage MessageToPortraitImage (McEmailMessage message)
        {
            if (0 == message.cachedPortraitId) {
                return ImageOfSender (message.AccountId, Pretty.EmailString (message.From));
            } else {
                return PortraitToImage (message.cachedPortraitId);
            }
        }

        public static bool PerformAction (string action, string number)
        {
            var uristr = String.Format ("{0}:{1}", action, Uri.EscapeDataString(number));
            return UIApplication.SharedApplication.OpenUrl (new Uri (uristr));
        }


        public static void ComplainAbout (string complaintTitle, string complaintMessage)
        {
            UIAlertView alert = new UIAlertView (complaintTitle, complaintMessage, null, "OK", null);
            alert.Show ();
        }

        public static string GetImage (string image)
        {
            if (UIScreen.MainScreen.Bounds.Height > 600 && UIScreen.MainScreen.Bounds.Height < 700) {
                return image + "-667h";
            } else {
                return image;
            }
        }

        public static UIImage MakeCheckmark (UIColor checkColor)
        {
            var size = new CGSize (15, 15);

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var g = UIGraphics.GetCurrentContext ();


            //set up drawing attributes
            g.SetLineWidth (1);

            checkColor.SetStroke ();

            //create geometry
            var checkmark = new CGPath ();

            checkmark.AddLines (new CGPoint[] {
                new CGPoint (0, 10),
                new CGPoint (5, 15), 
                new CGPoint (15, 0)
            });

            //checkmark.CloseSubpath ();

            //add geometry to graphics context and draw it
            g.AddPath (checkmark);
            g.DrawPath (CGPathDrawingMode.Stroke);

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;

        }

        public static UIImage MakeArrow (UIColor arrowColor)
        {
            var size = new CGSize (15, 15);

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var g = UIGraphics.GetCurrentContext ();
            g.SetLineWidth (1);

            arrowColor.SetStroke ();
            var arrow = new CGPath ();

            arrow.AddLines (new CGPoint[] {
                new CGPoint (6, 2),
                new CGPoint (15, 8), 
                new CGPoint (6, 13)
            });
            g.AddPath (arrow);
            g.DrawPath (CGPathDrawingMode.Stroke);

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;

        }

        public static UIImageView AddArrowAccessory (nfloat xOffset, nfloat yOffset, nfloat size, UIView parentView = null)
        {
            UIImageView ArrowAcccessoryImage = new UIImageView (new CGRect (xOffset, yOffset, size, size));
            using (var image = UIImage.FromBundle ("gen-more-arrow")) {
                ArrowAcccessoryImage.Image = image;
            }
            if (null != parentView) {
                parentView.AddSubview (ArrowAcccessoryImage);
            }
            return ArrowAcccessoryImage;
        }

        public static UIView AddHorizontalLine (nfloat offset, nfloat yVal, nfloat width, UIColor color, UIView parentView = null)
        {
            var lineUIView = new UIView (new CGRect (offset, yVal, width, .5f));
            lineUIView.BackgroundColor = color;
            if (null != parentView) {
                parentView.Add (lineUIView);
            }
            return lineUIView;
        }

        public static UIView AddVerticalLine (nfloat offset, nfloat yVal, nfloat height, UIColor color, UIView parentView)
        {
            var lineUIView = new UIView (new CGRect (offset, yVal, .5f, height));
            lineUIView.BackgroundColor = color;
            parentView.Add (lineUIView);
            return lineUIView;
        }

        public static void ConfigureNavBar (bool isTransparent, UINavigationController nc)
        {
            if (isTransparent) {
                nc.NavigationBar.SetBackgroundImage (new UIImage (), UIBarMetrics.Default);
                nc.NavigationBar.ShadowImage = new UIImage ();
                nc.NavigationBar.Translucent = true;
                nc.NavigationBar.BackgroundColor = UIColor.Clear;
                nc.NavigationBar.TintColor = UIColor.White;
            } else {
                nc.NavigationBar.SetBackgroundImage (new UIImage (), UIBarMetrics.Default);
                nc.NavigationBar.ShadowImage = new UIImage ();
                nc.NavigationBar.Translucent = false;
                nc.NavigationBar.BackgroundColor = A.Color_NachoGreen;
                nc.NavigationBar.TintColor = A.Color_NachoBlue;
            }
        }

        public static void SetViewHeight (UIView view, nfloat height)
        {
            var frame = view.Frame;
            frame.Height = height;
            view.Frame = frame;
        }

        public static void SetAutomaticImageForButton (UIBarButtonItem button, string iconName)
        {
            using (var buttonImage = UIImage.FromBundle (iconName)) {
                button.Image = buttonImage.ImageWithRenderingMode (UIImageRenderingMode.Automatic);
            }
        }

        public static void SetAutomaticImageForButton (UIBarButtonItem button, UIImage image)
        {
            using (var buttonImage = image) {
                button.Image = buttonImage.ImageWithRenderingMode (UIImageRenderingMode.Automatic);
            }
        }

        public static void SetOriginalImageForButton (UIBarButtonItem button, string iconName)
        {
            using (var buttonImage = UIImage.FromBundle (iconName)) {
                button.Image = buttonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            }
        }

        public static void SetOriginalImagesForButton (UIButton button, string iconName, string activeIconName = null)
        {
            using (var rawImage = UIImage.FromBundle (iconName)) {
                using (var originalImage = rawImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                    button.SetImage (originalImage, UIControlState.Normal);
                }
            }
            if (null != activeIconName) {
                using (var rawImage = UIImage.FromBundle (activeIconName)) {
                    using (var originalImage = rawImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                        button.SetImage (originalImage, UIControlState.Selected);
                    }
                }
            }
        }

        public static void SetBackButton (UINavigationController nc, UINavigationItem ni, UIColor tintColor)
        {
            using (var image = UIImage.FromBundle ("nav-backarrow")) {
                UIBarButtonItem backButton = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, (sender, args) => {
                    nc.PopViewController (true);
                });
                backButton.AccessibilityLabel = "Back";
                backButton.TintColor = tintColor;
                ni.SetLeftBarButtonItem (backButton, true);
            }
        }

        public static void HideViewHierarchy (UIView view)
        {
            view.Hidden = true;
            foreach (var v in view.Subviews) {
                HideViewHierarchy (v);
            }
        }

        public static bool IsVisible (this UIViewController vc)
        {
            return(vc.IsViewLoaded && (null != vc.View.Window));
        }

        public static string GetVersionNumber ()
        {
            var bundle = NSBundle.MainBundle;
            var build = bundle.InfoDictionary ["CFBundleVersion"].ToString ();
            var version = bundle.InfoDictionary ["CFBundleShortVersionString"].ToString ();
            return String.Format ("{0} ({1})", version, build);
        }

        public static UITableView FindEnclosingTableView (UIView view)
        {
            while (null != view) {
                if (view is UITableView) {
                    return (view as UITableView);
                }
                view = view.Superview;
            }
            return null;
        }

        public static UITableViewCell FindEnclosingTableViewCell (UIView view)
        {
            while (null != view) {
                if (view is UITableViewCell) {
                    return (view as UITableViewCell);
                }
                view = view.Superview;
            }
            return null;
        }

        /// <summary>
        /// Finds the outermost enclosing view, ideally
        /// it is the view that covers the whole screen.
        /// </summary>
        public static UIView FindOutermostView (UIView view)
        {
            if (null == view) {
                return null;
            }
            if (null == view.Superview) {
                return view;
            }
            return FindOutermostView (view.Superview);
        }
            
        public static UIViewController FindOutermostViewController()
        {
            var appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
            var topVC = appDelegate.Window.RootViewController;
            while(null != topVC.PresentedViewController) {
                topVC = topVC.PresentedViewController;
            }
            return topVC;
        }

        public static UIColor FindSolidBackgroundColor (UIView v)
        {
            if (null == v) {
                return UIColor.White;
            }
            if (null != v.BackgroundColor) {
                if (1 == v.BackgroundColor.CGColor.Alpha) {
                    return v.BackgroundColor;
                }
            }
            return FindSolidBackgroundColor (v.Superview);
        }

        public static Stream GenerateStreamFromString (string s)
        {
            MemoryStream stream = new MemoryStream ();
            StreamWriter writer = new StreamWriter (stream);
            writer.Write (s);
            writer.Flush ();
            stream.Position = 0;
            return stream;
        }

        /// ///////////
        // Event View Helpers
        /// ///////////
        public static void AddButtonImage (UIButton button, string imageName, UIControlState buttonState)
        {
            using (var buttonImage = UIImage.FromBundle (imageName)) {
                using (var originalImage = buttonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                    button.SetImage (originalImage, buttonState);
                }
            }
        }

        public static void AddTextLabel (nfloat xOffset, nfloat yOffset, nfloat width, nfloat height, string text, UIView parentView)
        {
            var textLabel = new UILabel (new CGRect (xOffset, yOffset, width, height));
            textLabel.Text = text;
            textLabel.Font = A.Font_AvenirNextRegular14;
            textLabel.TextColor = A.Color_NachoLightText;
            parentView.AddSubview (textLabel);
        }

        public static UIView AddTextLabelWithImageView (nfloat yOffset, string text, string imageName, EventViewController.TagType tag, UIView parentView)
        {
            var view = new UIView (new CGRect (0, yOffset, parentView.Bounds.Width, 16));
            view.Tag = (int)tag;

            var textLabel = new UILabel (new CGRect (42, 0, 100, 16));
            textLabel.Text = text;
            textLabel.Font = A.Font_AvenirNextMedium12;
            textLabel.TextColor = A.Color_NachoLightText;
            view.AddSubview (textLabel);

            var imageView = new UIImageView (new CGRect (18, 0, 16, 16));
            using (var image = UIImage.FromBundle (imageName)) {
                imageView.Image = image;
            }
            view.AddSubview (imageView);

            parentView.AddSubview (view);
            return view;
        }

        public static UILabel AddDetailTextLabel (nfloat xOffset, nfloat yOffset, nfloat width, nfloat height, EventViewController.TagType tag, UIView parentView)
        {
            var textLabel = new UILabel (new CGRect (xOffset, yOffset, width, height));
            textLabel.Font = A.Font_AvenirNextRegular14;
            textLabel.TextColor = A.Color_NachoDarkText;
            textLabel.Tag = (int)tag;
            parentView.AddSubview (textLabel);
            return textLabel;
        }

        public static void CreateAttendeeButton (nfloat attendeeImageDiameter, nfloat spacing, nfloat titleOffset, McAttendee attendee, int attendeeNum, bool isOrganizer, UIView parentView)
        {
            var attendeeButton = UIButton.FromType (UIButtonType.RoundedRect);
            attendeeButton.Layer.CornerRadius = attendeeImageDiameter / 2;
            attendeeButton.Layer.MasksToBounds = true;
            attendeeButton.Frame = new CGRect (42 + spacing, 10 + titleOffset, attendeeImageDiameter, attendeeImageDiameter);
            var userImage = Util.ImageOfSender (LoginHelpers.GetCurrentAccountId (), attendee.Email);

            if (null != userImage) {
                using (var rawImage = userImage) {
                    using (var originalImage = rawImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                        attendeeButton.SetImage (originalImage, UIControlState.Normal);
                    }
                }
                attendeeButton.Layer.BorderWidth = .25f;
                attendeeButton.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            } else {
                attendeeButton.Font = A.Font_AvenirNextRegular17;
                attendeeButton.ShowsTouchWhenHighlighted = true;
                attendeeButton.SetTitleColor (UIColor.White, UIControlState.Normal);
                attendeeButton.SetTitleColor (UIColor.LightGray, UIControlState.Selected);
                attendeeButton.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_TAG + attendeeNum;
                attendeeButton.SetTitle (ContactsHelper.NameToLetters (attendee.DisplayName), UIControlState.Normal);
                attendeeButton.AccessibilityLabel = "Attendee";
                attendeeButton.Layer.BackgroundColor = Util.GetCircleColorForEmail (attendee.Email, LoginHelpers.GetCurrentAccountId ()).CGColor;
            }

            // There are future plans to do something with these buttons, but right now
            // they don't have any behavior.  So pass their events to the parent view.
            attendeeButton.UserInteractionEnabled = false;
            parentView.AddSubview (attendeeButton);

            var attendeeName = new UILabel (new CGRect (42 + spacing, 65 + titleOffset, attendeeImageDiameter, 15));
            attendeeName.Font = A.Font_AvenirNextRegular14;
            attendeeName.TextColor = UIColor.LightGray;
            attendeeName.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_LABEL_TAG + attendeeNum;
            attendeeName.TextAlignment = UITextAlignment.Center;
            attendeeName.Text = CalendarHelper.GetFirstName (attendee.DisplayName);
            parentView.AddSubview (attendeeName);

            // If the current user is the organizer, then construct a little circle in the
            // lower right corner of the main attendee circle, where the attendee's status
            // can be displayed.  If the user is not the organizer, then the attendees'
            // status is not known, so we don't want to display a blank circle.
            if (isOrganizer) {
                var responseView = new UIView (new CGRect (42 + spacing + 27, 37 + titleOffset, 20, 20));
                responseView.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_LABEL_TAG + attendeeNum + 200;
                responseView.BackgroundColor = UIColor.White;
                responseView.Layer.CornerRadius = 10;
                parentView.AddSubview (responseView);
                var circleView = new UIView (new CGRect (2.5f, 2.5f, 15, 15));
                circleView.BackgroundColor = UIColor.White;
                circleView.Layer.CornerRadius = 15 / 2;
                circleView.Layer.BorderColor = A.Color_NachoLightGrayBackground.CGColor;
                circleView.Layer.BorderWidth = 1;
                responseView.AddSubview (circleView);
                var responseImageView = new UIImageView (new CGRect (2.5f, 2.5f, 15, 15));
                responseImageView.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_LABEL_TAG + attendeeNum + 100;
                using (var image = GetImageForAttendeeResponse (attendee.AttendeeStatus)) {
                    if (null != image) {
                        responseImageView.Image = image;
                    }
                }
                responseView.AddSubview (responseImageView);
                responseView.Hidden = (null != responseImageView.Image ? false : true);
            }
        }


        /// <summary>
        /// Return the appropriate icon for the given attendee status.
        /// </summary>
        public static UIImage GetImageForAttendeeResponse (NcAttendeeStatus status)
        {
            switch (status) {
            case NcAttendeeStatus.Accept:
                return UIImage.FromBundle ("btn-mtng-accept-pressed");
            case NcAttendeeStatus.Tentative:
                return UIImage.FromBundle ("btn-mtng-tenative-pressed");
            case NcAttendeeStatus.Decline:
                return UIImage.FromBundle ("btn-mtng-decline-pressed");
            default:
                return null;
            }
        }

        /// ///////////
        /// ///////////

        public static void CallContact (McContact contact, Action<ContactDefaultSelectionViewController.DefaultSelectionType> selectDefault)
        {
            if (null == contact) {
                ComplainAbout ("No Phone Number", "This contact does not have a phone number.");
                return;
            }
            if (0 == contact.PhoneNumbers.Count) {
                if (contact.CanUserEdit ()) {
                    selectDefault (ContactDefaultSelectionViewController.DefaultSelectionType.PhoneNumberAdder);
                } else {
                    ComplainAbout ("No Phone Number", "This contact does not have a phone number, and we are unable to modify the contact.");
                }
            } else if (1 == contact.PhoneNumbers.Count) {
                if (!Util.PerformAction ("tel", contact.GetPrimaryPhoneNumber ())) {
                    ComplainAbout ("Cannot Dial", "We are unable to dial this phone number");
                }
            } else {
                foreach (var p in contact.PhoneNumbers) {
                    if (p.IsDefault) {
                        if (!Util.PerformAction ("tel", p.Value)) {
                            ComplainAbout ("Cannot Dial", "We are unable to dial this phone number");
                        }
                        return; 
                    }
                }
                selectDefault (ContactDefaultSelectionViewController.DefaultSelectionType.DefaultPhoneSelector);
            }
        }

        public static string GetContactDefaultEmail (McContact contact)
        {
            if (1 == contact.EmailAddresses.Count) {
                return contact.GetEmailAddress ();
            }
            foreach (var e in contact.EmailAddresses) {
                if (e.IsDefault) {
                    return e.Value;
                }
            }
            return null;
        }

        public static UIColor GetCircleColorForEmail (string displayEmailAddress, int accountId)
        {
            int colorIndex = 1;

            if (!String.IsNullOrEmpty (displayEmailAddress)) {
                McEmailAddress emailAddress;
                if (McEmailAddress.Get (accountId, displayEmailAddress, out emailAddress)) {
                    displayEmailAddress = emailAddress.CanonicalEmailAddress;
                    colorIndex = emailAddress.ColorIndex;
                }
            }

            return Util.ColorForUser (colorIndex);
        }

        public static void UpdateTable (UITableView tableView, List<int> adds, List<int> deletes)
        {
            var deletePaths = new List<NSIndexPath> ();
            if (null != deletes) {
                foreach (var i in deletes) {
                    var path = NSIndexPath.FromItemSection (i, 0);
                    deletePaths.Add (path);
                }
            }
            var addPaths = new List<NSIndexPath> ();
            if (null != adds) {
                foreach (var i in adds) {
                    addPaths.Add (NSIndexPath.FromItemSection (i, 0));
                }
            }
            if ((0 == deletePaths.Count) && (0 == addPaths.Count)) {
                tableView.ReloadData ();
                return;
            }
            tableView.BeginUpdates ();
            if (0 != deletePaths.Count) {
                tableView.DeleteRows (deletePaths.ToArray (), UITableViewRowAnimation.Fade);
            }
            if (0 != addPaths.Count) {
                tableView.InsertRows (addPaths.ToArray (), UITableViewRowAnimation.Top);
            }
            tableView.EndUpdates ();
        }

        public static bool AttributedStringEndsWith (NSAttributedString target, NSAttributedString match)
        {
            if (null == match) {
                return true;
            }
            if (null == target) {
                return (null == match);
            }
            if (target.Length < match.Length) {
                return false;
            }
            var t = target.Substring (target.Length - match.Length, match.Length);
            return match.IsEqual (t);
        }

        public static int ToMcModelIndex (this NSNumber number)
        {
            return number.Int32Value;
        }

        public static int ToArrayIndex (this nint n)
        {
            return (int)n;
        }

        public static NSDate ToNSDate (this DateTime dateTime)
        {
            return NSDate.FromTimeIntervalSinceReferenceDate ((dateTime - (new DateTime (2001, 1, 1, 0, 0, 0))).TotalSeconds);
        }

        public static DateTime ToDateTime (this NSDate nsDate)
        {
            // TODO: Why not just a cast?
            return (new DateTime (2001, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddSeconds (nsDate.SecondsSinceReferenceDate);
        }

        /// <summary>
        /// Set the minimum and maximum dates for a date picker.  By default, the range is from ten years
        /// in the past to 100 years in the future.  But extend that range as necessary so it includes the
        /// year surrounding a given date.
        /// </summary>
        public static void ConstrainDatePicker (UIDatePicker datePicker, DateTime referenceDate)
        {
            DateTime pickerMin = DateTime.UtcNow.AddYears (-10);
            DateTime pickerMax = DateTime.UtcNow.AddYears (100);
            if (DateTime.MinValue != referenceDate) {
                DateTime referenceMin = referenceDate.AddYears (-1);
                if (referenceMin < pickerMin) {
                    pickerMin = referenceMin;
                }
                DateTime referenceMax = referenceDate.AddYears (1);
                if (referenceMax > pickerMax) {
                    pickerMax = referenceMax;
                }
            }
            datePicker.MinimumDate = pickerMin.ToNSDate ();
            datePicker.MaximumDate = pickerMax.ToNSDate ();
        }

        public static string PrettyPointF (CGPoint p)
        {
            return String.Format ("({0},{1})", p.X, p.Y);
        }

        public static string PrettySizeF (CGSize s)
        {
            return String.Format ("({0},{1})", s.Width, s.Height);
        }

        public static string GetAccountServiceImageName (McAccount.AccountServiceEnum service)
        {
            string imageName;

            switch (service) {
            case McAccount.AccountServiceEnum.Exchange:
                imageName = "avatar-msexchange";
                break;
            case McAccount.AccountServiceEnum.GoogleDefault:
                imageName = "avatar-gmail";
                break;
            case McAccount.AccountServiceEnum.GoogleExchange:
                imageName = "avatar-googleapps";
                break;
            case McAccount.AccountServiceEnum.HotmailExchange:
                imageName = "avatar-hotmail";
                break;
            case McAccount.AccountServiceEnum.IMAP_SMTP:
                imageName = "avatar-imap";
                break;
            case McAccount.AccountServiceEnum.OutlookExchange:
                imageName = "avatar-outlook";
                break;
            case McAccount.AccountServiceEnum.Office365Exchange:
                imageName = "avatar-office365";
                break;
            case McAccount.AccountServiceEnum.Device:
                imageName = "avatar-iphone";
                break;
            case McAccount.AccountServiceEnum.iCloud:
                imageName = "avatar-icloud";
                break;
            case McAccount.AccountServiceEnum.Yahoo:
                imageName = "avatar-yahoo";
                break;
            case McAccount.AccountServiceEnum.Aol:
                imageName = "avatar-aol";
                break;
            case McAccount.AccountServiceEnum.SalesForce:
                imageName = "avatar-salesforce";
                break;
            default:
                imageName = "Icon";
                break;
            }
            return imageName;
        }

        public static UIImage ImageForAccount (McAccount account)
        {
            if (0 == account.DisplayPortraitId) {
                return UIImage.FromBundle (GetAccountServiceImageName (account.AccountService));
            } else {
                return Util.PortraitToImage (account.DisplayPortraitId);
            }
        }

        public static UIButton BlueButton (string title, nfloat frameWidth)
        {
            var rect = new CGRect (25, 0, frameWidth - 50, 46);
            var blueButton = new UIButton (rect);
            blueButton.BackgroundColor = A.Color_NachoSubmitButton;
            blueButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            blueButton.SetTitle (title, UIControlState.Normal);
            blueButton.TitleLabel.TextColor = UIColor.White;
            blueButton.TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            blueButton.Layer.CornerRadius = 4f;
            blueButton.Layer.MasksToBounds = true;
            blueButton.AccessibilityLabel = title;
            return blueButton;
        }

        // Rectangle for contents inside of rectangle at (0,0)
        public static CGRect CardContentRectangle (nfloat width, nfloat height)
        {
            return new CGRect (A.Card_Horizontal_Indent, A.Card_Vertical_Indent, width - (2 * A.Card_Horizontal_Indent), height);
        }

        /// <summary>
        /// Convert formatted text to plain text.
        /// </summary>
        /// <remarks>
        /// This really should be in a platform-generic location.  But the simplest code for doing the conversion
        /// is iOS-specific.  So this will sit in an iOS-specific class for now.
        /// </remarks>
        public static string ConvertToPlainText (string formattedText, NSDocumentType type)
        {
            try {
                NSError error = null;
                var descriptionData = NSData.FromString (formattedText);
                var descriptionAttributed = new NSAttributedString (descriptionData, new NSAttributedStringDocumentAttributes {
                    DocumentType = type
                }, ref error);
                return descriptionAttributed.Value;
            } catch (Exception e) {
                // The NSAttributedString init: routine will fail if formattedText is not the specified type.
                // We don't want to crash the app in this case.
                NachoCore.Utils.Log.Warn (NachoCore.Utils.Log.LOG_CALENDAR,
                    "Calendar body has unexpected format and will be treated as plain text: {0}", e.ToString());
                return formattedText;
            }
        }

        public static void SetHidden(bool hidden, params UIView[] views)
        {
            foreach(var view in views) {
                view.Hidden = hidden;
            }
        }


        #endregion
    }
}
