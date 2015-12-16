
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using MimeKit;
using NachoCore.ActiveSync;

namespace NachoClient.AndroidClient
{
    public interface IContactEditFragmentOwner
    {
        McContact ContactToView { get; }
    }

    public class ContactEditFragment : Fragment
    {
        private const int NOTE_REQUEST_CODE = 1;

        McContact contact;

        ButtonBar buttonBar;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactEditFragment, container, false);

            buttonBar = new ButtonBar (view);


            return view;
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);

            contact = ((IContactEditFragmentOwner)Activity).ContactToView;
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);

            switch (requestCode) {

            case NOTE_REQUEST_CODE:
                if (Result.Ok == resultCode) {
                    string newNoteText = NoteActivity.ModifiedNoteText (data);
                    if (null != newNoteText) {
                        ContactsHelper.SaveNoteText (contact, newNoteText);
                        contact = McContact.QueryById<McContact> (contact.Id);
                    }
                }
                break;
            }
        }

        public override void OnStart ()
        {
            base.OnStart ();

        }

        public override void OnPause ()
        {
            base.OnPause ();
        }

        public override void OnCreateContextMenu (IContextMenu menu, View v, IContextMenuContextMenuInfo menuInfo)
        {
            base.OnCreateContextMenu (menu, v, menuInfo);
            menu.Add ("Copy");
        }

        public override bool OnContextItemSelected (IMenuItem item)
        {
            ClipboardManager clipboard = (ClipboardManager)Activity.GetSystemService (Context.ClipboardService); 
            var info = (Android.Widget.AdapterView.AdapterContextMenuInfo)item.MenuInfo;
            var tv = info.TargetView.FindViewById<TextView> (Resource.Id.value);
            clipboard.Text = tv.Text;
            return true;
        }
    }

}
