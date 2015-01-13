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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.CoreLocation;
using System.Globalization;
using System.Drawing;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using System.Collections.Generic;
using MonoTouch.CoreGraphics;
using NachoClient.iOS;
using MonoTouch.CoreText;

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

     
        public class PhoneAttributeComparer: IComparer<McContactStringAttribute>
        {
            public int Compare (McContactStringAttribute x, McContactStringAttribute y)
            {
                ContactsHelper contactHelper = new ContactsHelper ();
                int xPriority = contactHelper.PhoneNames.IndexOf (x.Name);
                int yPriority = contactHelper.PhoneNames.IndexOf (y.Name);

                return xPriority.CompareTo (yPriority);
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

        static UIActionSheet sheet;

        public static UIActionSheet GetSheet (string title)
        {
            sheet = new UIActionSheet (title);
            return sheet;
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

        public static int PickRandomColorForUser ()
        {
            int randomNumber = random.Next (2, colors.Count);
            return randomNumber;
        }

        public static UIColor ColorForUser (int index)
        {
            NcAssert.True (0 < index);
            return colors [index];
        }

        public static UIColor GetContactColor (McContact contact)
        {
            if (0 == contact.CircleColor) {
                contact.CircleColor = PickRandomColorForUser ();
                contact.Update ();
            }

            return ColorForUser (contact.CircleColor);
        }

        public static NachoTabBarController GetActiveTabBar ()
        {
            var appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;

            NachoTabBarController activeTabBar;
            if (appDelegate.Window.RootViewController is NachoTabBarController) {
                activeTabBar = (NachoTabBarController)appDelegate.Window.RootViewController;
            } else if (null != appDelegate.Window.RootViewController.PresentedViewController.TabBarController) {
                activeTabBar = (NachoTabBarController)appDelegate.Window.RootViewController.PresentedViewController.TabBarController;
            } else {
                activeTabBar = (NachoTabBarController)appDelegate.Window.RootViewController.PresentedViewController;
            }
            NcAssert.NotNull (activeTabBar);

            return activeTabBar;
        }

        public static UIImage DotWithColor (UIColor color)
        {
            UIGraphics.BeginImageContext (new SizeF (22, 22));
            var ctx = UIGraphics.GetCurrentContext ();

            ctx.SetFillColor (color.CGColor);
            ctx.FillEllipseInRect (new RectangleF (5, 5, 12, 12));

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;
        }

        public static UIImage DrawCalDot (UIColor circleColor, SizeF size)
        {
            var origin = new PointF (0, 0);

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var ctx = UIGraphics.GetCurrentContext ();

            ctx.SetFillColor (circleColor.CGColor);
            ctx.FillEllipseInRect (new RectangleF (origin, size));

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;
        }

        public static UIImage DrawTodayButtonImage (string day)
        {
            var size = new SizeF (24, 24);
            var origin = new PointF (0, 0);

            var todayImage = UIImage.FromBundle ("calendar-empty-cal-alt");

            var attributedString = new NSAttributedString (day,
                                       new UIStringAttributes {
                    Font = A.Font_AvenirNextMedium12
                });

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var ctx = UIGraphics.GetCurrentContext ();
            ctx.TranslateCTM (0, todayImage.Size.Height);
            ctx.ScaleCTM (1, -1);
            ctx.DrawImage (new RectangleF (origin, size), todayImage.CGImage);

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

        public static void CacheUserMessageFields (McEmailMessage emailMessage)
        {
            int ColorIndex;
            string Initials;
            UserMessageField (emailMessage.From, emailMessage.AccountId, out ColorIndex, out Initials);
            emailMessage.cachedFromColor = ColorIndex;
            emailMessage.cachedFromLetters = Initials;
            emailMessage.Update ();
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
            // Let create the initials
            McContact contact = new McContact ();
            NcEmailAddress.SplitName (mailboxAddress, ref contact);
            // Using the name
            string initials = "";
            if (!String.IsNullOrEmpty (contact.FirstName)) {
                initials += Char.ToUpper (contact.FirstName [0]);
            }
            if (!String.IsNullOrEmpty (contact.LastName)) {
                initials += Char.ToUpper (contact.LastName [0]);
            }
            // Or, failing that, the first char
            if (String.IsNullOrEmpty (initials)) {
                if (!String.IsNullOrEmpty (from)) {
                    foreach (char c in from) {
                        if (Char.IsLetterOrDigit (c)) {
                            initials += Char.ToUpper (c);
                            break;
                        }
                    }
                }
            }
            // Save it to the db
            Initials = initials;
        }

        public static UIImage ImageOfContact (McContact contact)
        {
            if (null == contact) {
                return null;
            }
            if (0 == contact.PortraitId) {
                return null;
            }
            return ImageOfPortrait (contact.PortraitId);
        }

        public static UIImage ImageOfPortrait (int portraitId)
        {
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
                UIImage image = ImageOfContact (contact);
                if (null != image) {
                    return image;
                }
            }
            return null;
        }

        public static UIImage PortraitOfSender (McEmailMessage message)
        {
            if (0 == message.cachedPortraitId) {
                return null;
            }
            var image = ImageOfPortrait (message.cachedPortraitId);
            if (null == image) {
                message.cachedPortraitId = 0;
                message.Update ();
            }
            return image;
        }

        public static void PerformAction (string action, string number)
        {
            UIApplication.SharedApplication.OpenUrl (new Uri (String.Format ("{0}:{1}", action, number)));
        }


        public static void ComplainAbout (string complaintTitle, string complaintMessage)
        {
            UIAlertView alert = new UIAlertView (complaintTitle, complaintMessage, null, "OK", null);
            alert.Show ();
        }

        public static string NameToLetters (string name)
        {
            if (null == name) {
                return "";
            }
            var Initials = "";
            string[] names = name.Split (new char [] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (1 == names.Length) {
                Initials = (names [0].Substring (0, 1)).ToCapitalized ();
            }
            if (2 == names.Length) {
                if (0 < name.IndexOf (',')) {
                    // Last name, First name
                    Initials = (names [1].Substring (0, 1)).ToCapitalized () + (names [0].Substring (0, 1)).ToCapitalized ();
                } else {
                    // First name, Last name
                    Initials = (names [0].Substring (0, 1)).ToCapitalized () + (names [1].Substring (0, 1)).ToCapitalized ();
                }
            }
            if (2 < names.Length) {
                if (0 < name.IndexOf (',')) {
                    // Last name, First name
                    Initials = (names [1].Substring (0, 1)).ToCapitalized () + (names [0].Substring (0, 1)).ToCapitalized ();
                } else if (-1 == name.IndexOf (',')) {
                    if ((names [1].Substring (0, 1)).ToLower () != (names [1].Substring (0, 1))) {
                        Initials = (names [0].Substring (0, 1)).ToCapitalized () + (names [1].Substring (0, 1)).ToCapitalized ();
                    } else {
                        Initials = (names [0].Substring (0, 1)).ToCapitalized ();
                    }
                }
            }

            return Initials;
        }

        public static string GetFirstName (string displayName)
        {
            string[] names = displayName.Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (names [0] == null) {
                return "";
            }
            if (names [0].Length > 1) {
                return char.ToUpper (names [0] [0]) + names [0].Substring (1);
            }
            return names [0].ToUpper ();
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
            var size = new SizeF (15, 15);

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var g = UIGraphics.GetCurrentContext ();


            //set up drawing attributes
            g.SetLineWidth (1);

            checkColor.SetStroke ();

            //create geometry
            var checkmark = new CGPath ();

            checkmark.AddLines (new PointF[] {
                new PointF (0, 10),
                new PointF (5, 15), 
                new PointF (15, 0)
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
            var size = new SizeF (15, 15);

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var g = UIGraphics.GetCurrentContext ();
            g.SetLineWidth (1);

            arrowColor.SetStroke ();
            var arrow = new CGPath ();

            arrow.AddLines (new PointF[] {
                new PointF (6, 2),
                new PointF (15, 8), 
                new PointF (6, 13)
            });
            g.AddPath (arrow);
            g.DrawPath (CGPathDrawingMode.Stroke);

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;

        }

        public static void AddArrowAccessory (float xOffset, float yOffset, float size, UIView parentView)
        {
            UIImageView ArrowAcccessoryImage = new UIImageView (new RectangleF (xOffset, yOffset, size, size));
            using (var image = UIImage.FromBundle ("gen-more-arrow")) {
                ArrowAcccessoryImage.Image = image;
            }
            parentView.AddSubview (ArrowAcccessoryImage);
        }

        public static UIView AddHorizontalLineView (float offset, float yVal, float width, UIColor color)
        {
            var lineUIView = new UIView (new RectangleF (offset, yVal, width, .5f));
            lineUIView.BackgroundColor = color;
            return lineUIView;
        }

        public static UIView AddHorizontalLine (float offset, float yVal, float width, UIColor color, UIView parentView)
        {
            var lineUIView = new UIView (new RectangleF (offset, yVal, width, .5f));
            lineUIView.BackgroundColor = color;
            parentView.Add (lineUIView);
            return lineUIView;
        }

        public static UIView AddVerticalLine (float offset, float yVal, float height, UIColor color, UIView parentView)
        {
            var lineUIView = new UIView (new RectangleF (offset, yVal, .5f, height));
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

        public static void SetViewHeight (UIView view, float height)
        {
            var frame = view.Frame;
            frame.Height = height;
            view.Frame = frame;
        }

        public static string GlobalObjIdToUID (string GlobalObjId)
        {
            string UID;
            bool OutlookID = false;

            byte[] data = Convert.FromBase64String (GlobalObjId);

            StringBuilder sb = new StringBuilder ();
            for (int i = 40; i < 48; i++) {
                sb.Append (Convert.ToChar (data [i]));
            }
            string vCalHolder = sb.ToString ();

            int uidHolderLength = 0;
            for (int i = 36; i < 40; i++) {
                uidHolderLength += data [i];
            }

            int remainingLength = 0;
            for (int i = 40; i < data.Length; i++) {
                remainingLength += 1;
            }

            if (53 > data.Length) {
                OutlookID = true;
            } else if ("vCal-Uid" != vCalHolder) {
                OutlookID = true;
            } else if (13 > uidHolderLength || remainingLength < uidHolderLength) {
                OutlookID = true;
            }

            if (OutlookID) {
                for (int i = 16; i < 20; i++) {
                    data [i] = 0;
                }
                UID = BitConverter.ToString (data);
            } else {
                sb.Clear ();
                int uidLength = uidHolderLength - 13;
                for (int i = 0; i < uidLength; i++) {
                    sb.Append (Convert.ToChar (data [52 + i]));
                }
                UID = sb.ToString ();
            }

            UID = UID.Replace ("-", "");
            UID = UID.Replace ("{", "");
            UID = UID.Replace ("}", "");
            return UID;
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

        public static void SetOriginalImagesForButton (UIButton button, string iconName, string activeIconName)
        {
            using (var rawImage = UIImage.FromBundle (iconName)) {
                using (var originalImage = rawImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                    button.SetImage (originalImage, UIControlState.Normal);
                }
            }
            using (var rawImage = UIImage.FromBundle (activeIconName)) {
                using (var originalImage = rawImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                    button.SetImage (originalImage, UIControlState.Selected);
                }
            }
        }

        public static void SetBackButton (UINavigationController nc, UINavigationItem ni, UIColor tintColor)
        {
            using (var image = UIImage.FromBundle ("nav-backarrow")) {
                UIBarButtonItem backButton = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, (sender, args) => {
                    nc.PopViewControllerAnimated (true);
                });
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

        public static void HideBlackNavigationControllerLine (UINavigationBar navBar)
        {
            navBar.SetBackgroundImage (new UIImage (), UIBarMetrics.Default);
            navBar.ShadowImage = new UIImage ();
        }

        public static bool IsVisible (this UIViewController vc)
        {
            return(vc.IsViewLoaded && (null != vc.View.Window));
        }

        public static string GetVersionNumber ()
        {
            var devBundleId = NSBundle.FromIdentifier ("com.nachocove.nachomail");
            var betaBundleId = NSBundle.FromIdentifier ("com.nachocove.nachomail.beta");
            var alphaBundleId = NSBundle.FromIdentifier ("com.nachocove.nachomail.alpha");

            if (devBundleId != null) {
                var build = devBundleId.InfoDictionary ["CFBundleVersion"];
                var version = devBundleId.InfoDictionary ["CFBundleShortVersionString"].ToString ();
                return String.Format ("{0} ({1})", version, build);
            } 
            if (betaBundleId != null) {
                var build = betaBundleId.InfoDictionary ["CFBundleVersion"];
                var version = betaBundleId.InfoDictionary ["CFBundleShortVersionString"].ToString ();
                return String.Format ("{0} ({1})", version, build);
            } 
            if (alphaBundleId != null) {
                var build = alphaBundleId.InfoDictionary ["CFBundleVersion"];
                var version = alphaBundleId.InfoDictionary ["CFBundleShortVersionString"].ToString ();
                return String.Format ("{0} ({1})", version, build);
            } 
            return "Unknown version";
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
                button.SetImage (buttonImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal), buttonState);
            }
        }

        public static void AddTextLabel (float xOffset, float yOffset, float width, float height, string text, UIView parentView)
        {
            var textLabel = new UILabel (new RectangleF (xOffset, yOffset, width, height));
            textLabel.Text = text;
            textLabel.Font = A.Font_AvenirNextRegular14;
            textLabel.TextColor = A.Color_NachoLightText;
            parentView.AddSubview (textLabel);
        }

        public static void AddTextLabelWithImageView (float yOffset, string text, string imageName, EventViewController.TagType tag, UIView parentView)
        {
            var view = new UIView (new RectangleF (0, yOffset, parentView.Frame.Width, 16));
            view.Tag = (int)tag;

            var textLabel = new UILabel (new RectangleF (42, 0, 100, 16));
            textLabel.Text = text;
            textLabel.Font = A.Font_AvenirNextMedium12;
            textLabel.TextColor = A.Color_NachoLightText;
            view.AddSubview (textLabel);

            var imageView = new UIImageView (new RectangleF (18, 0, 16, 16));
            using (var image = UIImage.FromBundle (imageName)) {
                imageView.Image = image;
            }
            view.AddSubview (imageView);

            parentView.AddSubview (view);
        }

        public static void AddDetailTextLabel (float xOffset, float yOffset, float width, float height, EventViewController.TagType tag, UIView parentView)
        {
            var textLabel = new UILabel (new RectangleF (xOffset, yOffset, width, height));
            textLabel.Font = A.Font_AvenirNextRegular14;
            textLabel.TextColor = A.Color_NachoDarkText;
            textLabel.Tag = (int)tag;
            parentView.AddSubview (textLabel);
        }

        public static void CreateAttendeeButton (float attendeeImageDiameter, float spacing, float titleOffset, McAttendee attendee, int attendeeNum, bool isOrganizer, UIView parentView)
        {
            var attendeeButton = UIButton.FromType (UIButtonType.RoundedRect);
            attendeeButton.Layer.CornerRadius = attendeeImageDiameter / 2;
            attendeeButton.Layer.MasksToBounds = true;
            attendeeButton.Frame = new RectangleF (42 + spacing, 10 + titleOffset, attendeeImageDiameter, attendeeImageDiameter);
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
                attendeeButton.SetTitle (Util.NameToLetters (attendee.DisplayName), UIControlState.Normal);
                attendeeButton.Layer.BackgroundColor = Util.GetCircleColorForEmail (attendee.Email, LoginHelpers.GetCurrentAccountId ()).CGColor;
            }

            // There are future plans to do something with these buttons, but right now
            // they don't have any behavior.  So pass their events to the parent view.
            attendeeButton.UserInteractionEnabled = false;
            parentView.AddSubview (attendeeButton);

            var attendeeName = new UILabel (new RectangleF (42 + spacing, 65 + titleOffset, attendeeImageDiameter, 15));
            attendeeName.Font = A.Font_AvenirNextRegular14;
            attendeeName.TextColor = UIColor.LightGray;
            attendeeName.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_LABEL_TAG + attendeeNum;
            attendeeName.TextAlignment = UITextAlignment.Center;
            attendeeName.Text = Util.GetFirstName (attendee.DisplayName);
            parentView.AddSubview (attendeeName);

            // If the current user is the organizer, then construct a little circle in the
            // lower right corner of the main attendee circle, where the attendee's status
            // can be displayed.  If the user is not the organizer, then the attendees'
            // status is not known, so we don't want to display a blank circle.
            if (isOrganizer) {
                var responseView = new UIView (new RectangleF (42 + spacing + 27, 37 + titleOffset, 20, 20));
                responseView.Tag = (int)EventViewController.TagType.EVENT_ATTENDEE_LABEL_TAG + attendeeNum + 200;
                responseView.BackgroundColor = UIColor.White;
                responseView.Layer.CornerRadius = 10;
                parentView.AddSubview (responseView);
                var circleView = new UIView (new RectangleF (2.5f, 2.5f, 15, 15));
                circleView.BackgroundColor = UIColor.White;
                circleView.Layer.CornerRadius = 15 / 2;
                circleView.Layer.BorderColor = A.Color_NachoLightGrayBackground.CGColor;
                circleView.Layer.BorderWidth = 1;
                responseView.AddSubview (circleView);
                var responseImageView = new UIImageView (new RectangleF (2.5f, 2.5f, 15, 15));
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

        public static void CallContact (string segueIdentifier, McContact contact, NcUIViewController owner)
        {
            if (0 == contact.PhoneNumbers.Count) {
                owner.PerformSegue (segueIdentifier, new SegueHolder (contact, ContactDefaultSelectionViewController.DefaultSelectionType.PhoneNumberAdder));
            } else if (1 == contact.PhoneNumbers.Count) {
                Util.PerformAction ("tel", contact.GetPrimaryPhoneNumber ());
            } else {
                foreach (var p in contact.PhoneNumbers) {
                    if (p.IsDefault) {
                        Util.PerformAction ("tel", p.Value);
                        return; 
                    }
                }
                owner.PerformSegue (segueIdentifier, new SegueHolder (contact, ContactDefaultSelectionViewController.DefaultSelectionType.DefaultPhoneSelector));
            }
        }

        public static void EmailContact (string segueIdentifier, McContact contact, NcUIViewController owner)
        {
            if (0 == contact.EmailAddresses.Count) {
                owner.PerformSegue (segueIdentifier, new SegueHolder (contact, ContactDefaultSelectionViewController.DefaultSelectionType.EmailAdder));
            } else if (1 == contact.EmailAddresses.Count) {
                owner.PerformSegue ("SegueToMessageCompose", new SegueHolder (contact.GetEmailAddress ()));
            } else {
                foreach (var e in contact.EmailAddresses) {
                    if (e.IsDefault) {
                        owner.PerformSegue ("SegueToMessageCompose", new SegueHolder (e.Value));
                        return;
                    }
                }
                owner.PerformSegue (segueIdentifier, new SegueHolder (contact, ContactDefaultSelectionViewController.DefaultSelectionType.DefaultEmailSelector));
            }
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
                tableView.InsertRows (addPaths.ToArray (), UITableViewRowAnimation.Fade);
            }
            tableView.EndUpdates ();
        }

        #endregion
    }
}
