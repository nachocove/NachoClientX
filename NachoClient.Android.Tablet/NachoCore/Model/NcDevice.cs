using System;
using System.Text;
using System.Text.RegularExpressions;
#if __IOS__
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using UIDeviceHelper;
#endif

namespace NachoCore.Model
{
	public class NcDevice
	{
#if __IOS__
		public static bool IsSimulator() {
			return UIDevice.CurrentDevice.Model.Contains ("Simulator");
		}
		public static string Type() {
			return UIDevice.CurrentDevice.Model.Split (null)[0];
		}
		public static string Model() {
			if (IsSimulator ()) {
				return "iPhone5C2";
			}
			return UIDevice.CurrentDevice.Platform().Replace (',', 'C');
		}
		public static string Os() {
			if (IsSimulator ()) {
				return "iOS 6.1.4 10B350";
			}
			return "iOS " + UIDevice.CurrentDevice.SystemVersion + ' ' + UIDevice.CurrentDevice.Build ();
		}
		public static string OsLanguage() {
			// FIXME - we will need to update (a) to handle locale, (b) to take into account our bundle (possibly).
			return "en-us";
			//return NSUserDefaults.StandardUserDefaults.StringForKey ("AppleLanguages");
		}
		public static string FriendlyName() {
			return UIDevice.CurrentDevice.Name;
		}
		public static string UserAgent() {
			if (IsSimulator ()) {
				return "Apple-iPhone5C2/1002.350";
			}
			var rawBuild = UIDevice.CurrentDevice.Build ();
			string letter = Regex.Match (rawBuild, "[A-Z]").Value;
			string[] sides = Regex.Split (rawBuild, "[A-Z]");
			var lhs = ((Convert.ToInt32 (sides [0]) * 100) + 
				Encoding.ASCII.GetBytes (letter) [0] -
				Encoding.ASCII.GetBytes ("A") [0] + 1).ToString ();
			return "Apple-" + Type () + "/" + lhs + "." + sides [1];
		}
		public static string Identity() {
			// FIXME - hard wired for the moment. iOS Mail App uses 'Appl' + serial number: ApplF17K1P5BF8H4.
			// We can get the serial number, but it may not be kosher w/Apple. If we cant use serial number, 
			// then use either dev or adv uuid replacement.
			// https://github.com/erica/iOS-6-Cookbook
			return "ApplF17K1P5BF8H3";
		}
#endif
#if __ANDROID__
		public static bool IsSimulator() {
			return true;
		}
		public static string Type() {
			return "iPhone";
		}
		public static string Model() {
			return "iPhone5C2";
		}
		public static string Os() {
			return "iOS 6.1.4 10B350";
		}
		public static string OsLanguage() {
			return "en-us";
		}
		public static string FriendlyName() {
			return "CandyBar";
		}
		public static string UserAgent() {
			return "Apple-iPhone5C2/1002.350";
		}
		public static string Identity() {
			return "ApplF17K1P5BF8H3";
		}
#endif
	}
}

