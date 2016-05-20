// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using AppKit;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.Mac
{

    public interface AccountTypeViewDelegate {

        void AccountTypeViewDidSelectService (McAccount.AccountServiceEnum service);

    }

    public partial class AccountTypeViewController : NSViewController, INSTableViewDelegate, INSTableViewDataSource
    {

        WeakReference<AccountTypeViewDelegate> _AccountDelegate;
        public AccountTypeViewDelegate AccountDelegate {
            get {
                AccountTypeViewDelegate d;
                if (_AccountDelegate.TryGetTarget(out d)){
                    return d;
                }
                return null;
            }
            set {
                _AccountDelegate = new WeakReference<AccountTypeViewDelegate>(value);
            }
        }

        protected static McAccount.AccountServiceEnum[] DefaultAccountTypes = new McAccount.AccountServiceEnum[] {
            McAccount.AccountServiceEnum.Exchange,
            McAccount.AccountServiceEnum.GoogleDefault,
            McAccount.AccountServiceEnum.GoogleExchange,
            McAccount.AccountServiceEnum.HotmailExchange,
            McAccount.AccountServiceEnum.iCloud,
            McAccount.AccountServiceEnum.IMAP_SMTP,
            McAccount.AccountServiceEnum.Office365Exchange,
            McAccount.AccountServiceEnum.OutlookExchange,
            McAccount.AccountServiceEnum.Yahoo,
        };

		public AccountTypeViewController (IntPtr handle) : base (handle)
		{
		}

        [Export ("numberOfRowsInTableView:")]
        public System.nint GetRowCount (NSTableView tableView)
        {
            return DefaultAccountTypes.Length;
        }

        [Export ("tableView:viewForTableColumn:row:")]
        public AppKit.NSView GetViewForItem (NSTableView tableView, NSTableColumn tableColumn, nint row)
        {
            var accountType = DefaultAccountTypes [row];
            var view = tableView.MakeView ("AccountType", this) as NSTableCellView;
            view.TextField.StringValue = NcServiceHelper.AccountServiceName (accountType);
            return view;
        }

        [Export ("tableViewSelectionDidChange:")]
        public void SelectionDidChange (Foundation.NSNotification notification)
        {
            var accountType = DefaultAccountTypes [TableView.SelectedRow];
            AccountTypeViewDelegate accountDelegate;
            if (_AccountDelegate.TryGetTarget (out accountDelegate)) {
                accountDelegate.AccountTypeViewDidSelectService (accountType);
            }
        }

	}
}
