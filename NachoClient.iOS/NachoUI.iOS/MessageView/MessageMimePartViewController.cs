//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using MimeKit;

namespace NachoClient.iOS
{
    public class MessageMimePartViewController : NachoTableViewController
    {

        private const string BasicCellIdentifier = "BasicCellIdentifier";

        public Multipart Part;

        public MessageMimePartViewController () : base (UITableViewStyle.Grouped)
        {
        }

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.BackgroundColor = A.Color_NachoBackgroundGray;
            TableView.RegisterClassForCellReuse (typeof(SwipeTableViewCell), BasicCellIdentifier);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        #region Table Delegate & Data Source

        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            return Part.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var child = Part [indexPath.Row];
            var cell = tableView.DequeueReusableCell (BasicCellIdentifier) as SwipeTableViewCell;
            cell.TextLabel.Text = child.ContentType.ToString ();
            var childPart = child as MimePart;
            if (childPart != null && childPart.FileName != null) {
                cell.DetailTextLabel.Text = childPart.FileName;
            } else {
                cell.DetailTextLabel.Text = "";
            }
            cell.TextLabel.Font = A.Font_AvenirNextRegular14;
            cell.DetailTextLabel.Font = A.Font_AvenirNextRegular12;
            cell.DetailTextLabel.TextColor = A.Color_NachoTextGray;
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var child = Part [indexPath.Row];
            var multipart = child as Multipart;
            if (multipart != null) {
                var viewController = new MessageMimePartViewController ();
                viewController.Part = multipart;
                NavigationController.PushViewController (viewController, animated: true);
            } else {
                var part = child as MimePart;
                var viewController = new MessageRawBodyViewController ();
                using (var stream = new System.IO.MemoryStream ()) {
                    part.WriteTo (stream);
                    viewController.BodyContents = System.Text.UTF8Encoding.UTF8.GetString (stream.ToArray ());
                }
                NavigationController.PushViewController (viewController, animated: true);
            }
        }

        #endregion

    }
}

