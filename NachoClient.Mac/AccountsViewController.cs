// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;

using Foundation;
using AppKit;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.Mac
{
    public partial class AccountsViewController : NSViewController, INSTableViewDataSource, INSTableViewDelegate, INSAlertDelegate
	{
        List<McAccount> Accounts;

		public AccountsViewController (IntPtr handle) : base (handle)
		{
		}

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            Accounts = McAccount.GetAllConfiguredNormalAccounts ();
            RemoveButton.Enabled = false;
        }

        [Export ("numberOfRowsInTableView:")]
        public nint GetRowCount (NSTableView tableView)
        {
            return Accounts.Count;
        }

        [Export ("tableView:viewForTableColumn:row:")]
        public NSView GetViewForItem (NSTableView tableView, NSTableColumn tableColumn, nint row)
        {
            var account = Accounts [(int)row];
            var view = tableView.MakeView ("AccountCell", this) as NSTableCellView;
            view.TextField.StringValue = account.DisplayName;
            return view;
        }

        [Export ("tableViewSelectionDidChange:")]
        public void SelectionDidChange (NSNotification notification)
        {
            RemoveButton.Enabled = TableView.SelectedRow >= 0;
        }

        partial void AddAccount (NSObject sender)
        {
        }

        partial void RemoveAccount (NSObject sender)
        {
            int row = (int)TableView.SelectedRow;
            var account = Accounts [row];
            var alert = NSAlert.WithMessage (String.Format ("Deleting {0} ({1}) will permanently remove all of its information from this computer.", account.DisplayName, account.EmailAddr), "Delete Account", "Cancel", null, "");
            alert.Delegate = this;
            alert.BeginSheet (View.Window, (NSModalResponse response) => {
                if (response == NSModalResponse.OK){
                    Accounts.RemoveAt (row);
                    TableView.RemoveRows (NSIndexSet.FromIndex(row), NSTableViewAnimation.None);
                    Action action = () => {
                        NcAccountHandler.Instance.RemoveAccount (account.Id);
                        if (Accounts.Count == 0){
                            InvokeOnMainThread (() => {
                                View.Window.Close ();
                                (NSApplication.SharedApplication.Delegate as AppDelegate).ShowWelcome ();
                            });
                        }
                    };
                    NcTask.Run (action, "RemoveAccount");
                }
                alert.Window.Close ();
            });
        }

	}
}