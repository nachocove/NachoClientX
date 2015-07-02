//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using Foundation;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public static class MessageTableViewConstants
    {
        static public readonly nfloat NORMAL_ROW_HEIGHT = 126.0f;
        static public readonly nfloat DATED_ROW_HEIGHT = 161.0f;
    }

    public interface IMessageTableViewSource
    {
        void SetEmailMessages (INachoEmailMessages messageThreads, string messageWhenEmpty);

        void MultiSelectEnable (UITableView tableView);

        void MultiSelectCancel (UITableView tableView);

        void MultiSelectDelete (UITableView tableView);

        void MultiSelectArchive (UITableView tableView);

        void MultiSelectToggle (UITableView tableView);

        bool RefreshEmailMessages (out List<int> adds, out List<int> deletes);

        bool NoMessageThreads ();

        void ReconfigureVisibleCells (UITableView tableView);

        void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie);

        INachoEmailMessages GetNachoEmailMessages ();

        UITableViewSource GetTableViewSource ();

    }

    public interface IMessageTableViewSourceDelegate
    {
        void MessageThreadSelected (McEmailMessageThread thread);

        void PerformSegueForDelegate (string identifier, NSObject sender);

        void MultiSelectToggle (IMessageTableViewSource source, bool enabled);

        void MultiSelectChange (IMessageTableViewSource source, int count);
    }
}

