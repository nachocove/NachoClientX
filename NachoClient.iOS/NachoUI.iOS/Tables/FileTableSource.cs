//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;

namespace NachoClient.iOS
{
    public class FileTableSource : UITableViewDataSource
    {
        List<McFile> files;
        public UIColor cellTextColor = UIColor.Black;

        public FileTableSource ()
        {
            files = McFile.QueryAllFiles ();
        }

        public McFile GetFile(int i)
        {
            return files [i];
        }

        public override int RowsInSection (UITableView tableview, int section)
        {
            return files.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = tableView.DequeueReusableCell ("File");
            var file = files [indexPath.Row];
            cell.TextLabel.Text = file.DisplayName;
            cell.DetailTextLabel.Text = file.SourceApplication;
            cell.TextLabel.TextColor = cellTextColor;
            cell.BackgroundColor = UIColor.Clear;
            return cell;
        }
    }
}

