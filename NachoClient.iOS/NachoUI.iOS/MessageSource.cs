using System;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
	public class MessageSource : UIWebView

	{
		AppDelegate appDelegate { get; set; }

		public MessageSource ()
		{
			appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
		}

	
	}
}

