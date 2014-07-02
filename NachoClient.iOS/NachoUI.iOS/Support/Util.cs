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
using MonoTouch.Dialog;
using MonoTouch.CoreLocation;
using System.Globalization;
using System.Drawing;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using System.Collections.Generic;


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

        public static void ReportError (UIViewController current, Exception e, string msg)
        {
            if (current == null)
                throw new ArgumentNullException ("current");

            var root = new RootElement (Locale.GetText ("Error")) {
                new Section (Locale.GetText ("Error")) {
                    new StyledStringElement (msg) {
                        Font = UIFont.BoldSystemFontOfSize (14),
                    }
                }
            };

            if (e != null) {
                root.Add (new Section (e.GetType ().ToString ()) {
                    new StyledStringElement (e.Message) {
                        Font = UIFont.SystemFontOfSize (14),
                    }
                });
                root.Add (new Section ("Stacktrace") {
                    new StyledStringElement (e.ToString ()) {
                        Font = UIFont.SystemFontOfSize (14),
                    }
                });
            }
            ;

            // Delay one second, as UIKit does not like to present
            // views in the middle of an animation.
            NSTimer.CreateScheduledTimer (TimeSpan.FromSeconds (1), delegate {
                UINavigationController nav = null;
                DialogViewController dvc = new DialogViewController (root);
                dvc.NavigationItem.LeftBarButtonItem = new UIBarButtonItem (Locale.GetText ("Close"), UIBarButtonItemStyle.Plain, delegate {
                    nav.DismissViewController (false, null);
                });

                nav = new UINavigationController (dvc);
                current.PresentViewController (nav, false, null);
            });
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
            
        public static RootElement MakeProgressRoot (string caption)
        {
            return new RootElement (caption) {
                new Section () {
                    new ActivityElement ()
                }
            };
        }

        public static RootElement MakeError (string diagMsg)
        {
            return new RootElement (Locale.GetText ("Error")) {
                new Section (Locale.GetText ("Error")) {
                    new MultilineElement (Locale.GetText ("Unable to retrieve the information"))
                }
            };
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

        //Colors for users' CircleColor. These colors arent great.  NEED TO BE CHANGED
        public static UIColor greenColor = new UIColor (85.0f / 255.0f, 213.0f / 255.0f, 80.0f / 255.0f, 1.0f);
        public static UIColor redColor = new UIColor (232.0f / 255.0f, 61.0f / 255.0f, 14.0f / 255.0f, 1.0f);
        public static UIColor yellowColor = new UIColor (254.0f / 255.0f, 217.0f / 255.0f, 56.0f / 255.0f, 1.0f);
        public static UIColor brownColor = new UIColor (206.0f / 255.0f, 149.0f / 255.0f, 98.0f / 255.0f, 1.0f);
        public static UIColor fiveColor = new UIColor (120.0f / 255.0f, 111.0f / 255.0f, 79.0f / 255.0f, 1.0f);
        public static UIColor sixColor = new UIColor (50.0f / 255.0f, 10.0f / 255.0f, 14.0f / 255.0f, 1.0f);
        public static UIColor sevenColor = new UIColor (10.0f / 255.0f, 217.0f / 255.0f, 150.0f / 255.0f, 1.0f);
        public static UIColor eightColor = new UIColor (34.0f / 255.0f, 20.0f / 255.0f, 98.0f / 255.0f, 1.0f);
        public static List<UIColor> colors = new List<UIColor> () {
            UIColor.Clear,
            UIColor.LightGray,
            greenColor,
            redColor,
            yellowColor,
            brownColor,
            fiveColor,
            sixColor,
            sevenColor,
            eightColor
        };

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

        /// <summary>
        /// builds the circle containing the sender's initials
        /// </summary>
        public static UIImage LettersWithColor (string from, UIColor circleColor, UIFont font)
        {
            var size = new SizeF (40, 40);
            var origin = new PointF (0, 0);
            var content = NameToLetters (from);

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var ctx = UIGraphics.GetCurrentContext ();

            var contentString = new NSString (content);
            var contentSize = contentString.StringSize (font);
            ctx.SetFillColor (circleColor.CGColor);
            ctx.FillEllipseInRect (new RectangleF (origin, size));
            var rect = new RectangleF (0, ((40 - contentSize.Height) / 2) + 0, 40, contentSize.Height);

            ctx.SetFillColorWithColor (UIColor.White.CGColor);
            new NSString (content).DrawString (rect, font, UILineBreakMode.WordWrap, UITextAlignment.Center);

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;
        }

        /// <summary>
        /// builds the circle containing the sender's initials
        /// </summary>
        public static UIImage NumbersWithColor (string number, UIColor circleColor, UIColor fontColor, UIFont font)
        {
            var size = new SizeF (40, 40);
            var origin = new PointF (0, 0);
            var content = number;

            UIGraphics.BeginImageContextWithOptions (size, false, 0);
            var ctx = UIGraphics.GetCurrentContext ();

            var contentString = new NSString (content);
            var contentSize = contentString.StringSize (font);
            ctx.SetFillColor (circleColor.CGColor);
            ctx.FillEllipseInRect (new RectangleF (origin, size));
            var rect = new RectangleF (0, ((40 - contentSize.Height) / 2) + 0, 40, contentSize.Height);

            ctx.SetFillColorWithColor (fontColor.CGColor);
            new NSString (content).DrawString (rect, font, UILineBreakMode.WordWrap, UITextAlignment.Center);

            var image = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return image;
        }

        /// <summary>
        /// Converts a users name into capitalized initials
        /// </summary>
        public static string NameToLetters (string name)
        {
            var Initials = "";
            string[] names = name.Split (new char [] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (1 == names.Length) {
                Initials = (names [0].Substring (0, 1)).ToCapitalized ();
            }
            if (2 == names.Length) {
                if (0 < name.IndexOf (',')) {
                    // Last name, First name
                    Initials = (names [1].Substring (0, 1)).ToCapitalized () + (names [0].Substring (0, 1)).ToCapitalized ();
                } 
                else {
                    // First name, Last name
                    Initials = (names [0].Substring (0, 1)).ToCapitalized () + (names [1].Substring (0, 1)).ToCapitalized ();
                }
            }
            if (2 < names.Length) {
                if (0 < name.IndexOf (',')) {
                    // Last name, First name
                    Initials = (names [1].Substring (0, 1)).ToCapitalized () + (names [0].Substring (0, 1)).ToCapitalized ();
                } 
                else if (-1 == name.IndexOf (',')) {
                    if ((names [1].Substring (0, 1)).ToLower () != (names [1].Substring (0, 1))) {
                        Initials = (names [0].Substring (0, 1)).ToCapitalized () + (names [1].Substring (0, 1)).ToCapitalized ();
                    } 
                    else {
                        Initials = (names [0].Substring (0, 1)).ToCapitalized ();
                    }
                }
            }

            return Initials;
        }

        /// <summary>
        /// Takes a screenshot of the view passed in and returns an image
        /// </summary>
        public static UIImage caputureView(UIView view) {
            UIGraphics.BeginImageContextWithOptions (view.Bounds.Size, false, 0.0f);
            view.Layer.RenderInContext (UIGraphics.GetCurrentContext ());
            var capturedImage = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return capturedImage;
        }

        /// <summary>
        /// Takes an int and returns a UIColor.  This is nessary because the db
        /// can't store UIColors
        /// </summary>
        public static UIColor IntToUIColor (int colorNum)
        {
            return colors [colorNum];
        }

        /// <summary>
        /// Sets the user image. Three cases: user has a picture, user has a CircleColor, 
        /// user has niether and gets a random CircleColor gets assigned and stored in the db.
        /// </summary>
        public static int SenderToCircle (int accountId, string emailAddress)
        {
            var query = McContact.QueryByEmailAddress (accountId, emailAddress);
            int circleColor = 0;
            Random random = new Random ();
            int randomNumber = random.Next (2, 10);
            foreach (var person in query) {
                var colorNum = person.CircleColor;
                if (person.Picture != null) {
                    //Todo
                } else if (colorNum > 0) {
                    circleColor = colorNum;  
                } else if (colorNum == 0) {
                    circleColor = randomNumber;
                    McContact.UpdateUserCircleColor (randomNumber, person.DisplayEmailAddress);
                }
            }
            return circleColor;
        }

        public static UIColor ColorOfSenderMap(int colorIndex)
        {
            return colors [colorIndex];
        }

        // FIXME: Store the color, no the index, in the db
        public static int ColorIndexOfSender(int accountId, string emailAddress)
        {
            var contacts = McContact.QueryLikeEmailAddress (accountId, emailAddress);
            if (0 == contacts.Count) {
                return 1; // no matches; return default color
            }
            foreach(var contact in contacts) {
                if(0 < contact.CircleColor) {
                    return contact.CircleColor;
                }
            }
            var random = new Random ();
            int randomNumber = random.Next (2, 10);
            foreach (var contact in contacts) {
                contact.CircleColor = randomNumber;
                contact.Update ();
            }
            return randomNumber;
        }

        public static UIImage ImageOfSender (int accountId, string emailAddress)
        {
            return null;
        }

        public static void HighPriority()        {

            NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "DraggingStarted");
                      NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                               Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_ViewScrollingStarted),
                               Account = ConstMcAccount.NotAccountSpecific,
                         });

        }

        public static void RegularPriority()        {
            NachoCore.Utils.Log.Info (NachoCore.Utils.Log.LOG_UI, "DecelerationEnded");
                       NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_ViewScrollingStopped),
                            Account = ConstMcAccount.NotAccountSpecific,
                           });
        }

       
       

        #endregion
    }
}
