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
    public class TaskTableSource : UITableViewDataSource
    {
        List<NcTaskIndex> tasks;
        public UIColor cellTextColor = UIColor.Black;

        public TaskTableSource ()
        {
//            var account = NcModel.Instance.Db.Table<McAccount> ().First ();
//            tasks = McTask.QueryAllTaskItems (account.Id);
        }

        public override int RowsInSection (UITableView tableview, int section)
        {
            return tasks.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = tableView.DequeueReusableCell ("Task");
            var task = tasks [indexPath.Row].GetTask ();
            cell.TextLabel.Text = task.Subject;
            cell.DetailTextLabel.Text = task.DueDate.ToString ();
            cell.TextLabel.TextColor = cellTextColor;
            cell.BackgroundColor = UIColor.Clear;
            return cell;
        }
    }
}

