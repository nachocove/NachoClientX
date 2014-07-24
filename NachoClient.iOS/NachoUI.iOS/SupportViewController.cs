// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using SWRevealViewControllerBinding;
using NachoCore.Utils;
using System.Collections.Generic;

namespace NachoClient.iOS
{
	public partial class SupportViewController : NcUITableViewController
	{
        const string SubmitLogText = "Submit a log";
        const string ContactByEmailText = "Contact us via email";
        const string SupportEmail = "support@nachocove.com";
        const string ContactByPhoneText = "Support Number: +1 (404) 436-2246";
        const string PhoneNumberLink = "telprompt://14044362246";
        const string ContactByPhoneDetailText = "Please have your problem and a way for us to contact you available when you call.";

        const string SupportToComposeSegueId = "SupportToEmailCompose";
        const string BasicCell = "BasicCell";
        const string SubtitleCell = "SubtitleCell";

        // this string is sent to Telemetry when the user sends a log so we can collect the log
        const string LogNotification = "USER_SENDING_LOG";
        const string ContactingSupportNotification = "USER_IS_CONTACTING_SUPPORT";

		public SupportViewController (IntPtr handle) : base (handle)
		{
		}

        public override int RowsInSection (UITableView tableview, int section)
        {
            return 3;
        }

        public override int NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();

            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton };

            this.TableView.TableFooterView = new UIView (new System.Drawing.RectangleF (0, 0, 0, 0));
        }

        public override void RowSelected (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
        {
            switch (indexPath.Row) {
            case 0:
                SubmitALog ();
                break;
            case 1:
                ContactViaEmail ();
                break;
            case 2:
                UIApplication.SharedApplication.OpenUrl (new NSUrl (PhoneNumberLink));
                break;
            }

            this.TableView.DeselectRow (indexPath, true);
        }

        public void SubmitALog ()
        {
            Log.Info (Log.LOG_UI, LogNotification);
            var messageContent = new Dictionary<string, string>()
            {
                { "subject", "Additional log information" },
            };

            UIAlertView alert = new UIAlertView (
                "Log sent", 
                "Would you like to send an email along with your log report?", 
                null, 
                "OK", 
                "Cancel"
            );
            alert.Clicked += (s, b) => {
                if (b.ButtonIndex == 0) {
                    PerformSegue (SupportToComposeSegueId, new SegueHolder (messageContent));
                }
            };
            alert.Show();
        }

        public void ContactViaEmail ()
        {
            Log.Info (Log.LOG_UI, ContactingSupportNotification);
            var telem = Telemetry.SharedInstance;
            var clientId = telem.GetUserName ();
            if (clientId != null) {
                Log.Info (Log.LOG_UI, clientId);
            } else {
                Log.Info (Log.LOG_UI, "ClientId was not found");
            }
            PerformSegue (SupportToComposeSegueId, new SegueHolder (null));
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals (SupportToComposeSegueId)) {
                var dc = (MessageComposeViewController)segue.DestinationViewController;
                NcEmailAddress address = new NcEmailAddress (NcEmailAddress.Kind.To, SupportEmail);

                var holder = sender as SegueHolder;
                var contents = (Dictionary<string, string>)holder.value;

                string subject = null;
                if (contents != null) {
                    contents.TryGetValue ("subject", out subject);
                }

                dc.SetEmailAddressAndTemplate (address, subject);
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifier {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = null;

            switch (indexPath.Row) {
            case 0:
                cell = tableView.DequeueReusableCell (BasicCell);
                NcAssert.True (null != cell);
                cell.TextLabel.Text = SubmitLogText;
                break;
            case 1:
                cell = tableView.DequeueReusableCell (SubtitleCell);
                NcAssert.True (null != cell);
                cell.TextLabel.Text = ContactByEmailText;
                cell.DetailTextLabel.Text = SupportEmail;
                cell.DetailTextLabel.TextColor = UIColor.LightGray;
                cell.DetailTextLabel.Font = A.Font_AvenirNextRegular12;
                cell.DetailTextLabel.LineBreakMode = UILineBreakMode.WordWrap;
                cell.DetailTextLabel.Lines = 0;
                break;
            case 2:
                cell = tableView.DequeueReusableCell (SubtitleCell);
                NcAssert.True (null != cell);
                cell.TextLabel.Text = ContactByPhoneText;
                cell.DetailTextLabel.Text = ContactByPhoneDetailText;
                cell.DetailTextLabel.TextColor = UIColor.LightGray;
                cell.DetailTextLabel.Font = A.Font_AvenirNextRegular12;
                cell.DetailTextLabel.LineBreakMode = UILineBreakMode.WordWrap;
                cell.DetailTextLabel.Lines = 0;
                break;
            }

            cell.TextLabel.TextColor = A.Color_NachoBlack;
            cell.TextLabel.Font = A.Font_AvenirNextRegular14;

            return cell;
        }

        public override string TitleForHeader (UITableView tableView, int section)
        {
            switch (section) {
            case 0:
                return @"NachoCove - Beta 1";
            }

            return null;
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            float height = 0;

            var textAttributesDict = new NSDictionary (UIStringAttributeKey.Font, A.Font_AvenirNextRegular14);
            var detailTextAttributesDict = new NSDictionary (UIStringAttributeKey.Font, A.Font_AvenirNextRegular12);
            UIStringAttributes textAttrib = new UIStringAttributes(textAttributesDict);
            UIStringAttributes detailTextAttrib = new UIStringAttributes(detailTextAttributesDict);

            NSString text = null;
            NSString detailText = null;
            switch (indexPath.Row) {
            case 0:
                text = new NSString (SubmitLogText);
                break;
            case 1:
                text = new NSString (ContactByEmailText);
                detailText = new NSString (SupportEmail);
                break;
            case 2:
                text = new NSString (ContactByPhoneText);
                detailText = new NSString (ContactByPhoneDetailText);
                break;
            }

            var textSize = text.GetSizeUsingAttributes (textAttrib);
            var rect = text.GetBoundingRect (new System.Drawing.SizeF (textSize.Width, 1000), NSStringDrawingOptions.UsesLineFragmentOrigin, textAttrib, null);

            if (detailText != null) {
                var detailRect = text.GetBoundingRect (new System.Drawing.SizeF (textSize.Width, 1000), NSStringDrawingOptions.UsesLineFragmentOrigin, 
                    detailTextAttrib, null);
                height += detailRect.Height;
            }

            return height + rect.Height + 30.0F;
        }
	}
}
