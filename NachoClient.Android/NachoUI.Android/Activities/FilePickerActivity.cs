//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

namespace NachoClient.AndroidClient
{
    [Activity ()]
    public class FilePickerActivity : NcActivity
    {

        public const string EXTRA_ATTACHMENT_ID = "NachoClient.AndroidClient.FilePickerActivity.EXTRA_ATTACHMENT_ID";

        #region Intents

        public static Intent BuildIntent (Context context)
        {
            var intent = new Intent (context, typeof (FilePickerActivity));
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;
        TabLayout TabLayout;
        TabLayout.Tab NameTab;
        TabLayout.Tab DateTab;
        TabLayout.Tab ContactTab;
        FilePickerFragment FilePickerFragment;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
			TabLayout = FindViewById (Resource.Id.tabs) as TabLayout;

			NameTab = TabLayout.NewTab ();
			NameTab.SetText (Resource.String.file_picker_tab_name);
			TabLayout.AddTab (NameTab);

			DateTab = TabLayout.NewTab ();
			DateTab.SetText (Resource.String.file_picker_tab_date);
			TabLayout.AddTab (DateTab);

			ContactTab = TabLayout.NewTab ();
			ContactTab.SetText (Resource.String.file_picker_tab_contact);
			TabLayout.AddTab (ContactTab);

            TabLayout.TabSelected += TabSelected;
        }

        void ClearSubviews ()
        {
            TabLayout.TabSelected -= TabSelected;
            Toolbar = null;
            TabLayout = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.FilePickerActivity);
			FindSubviews ();
			SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
        }

        public override void OnAttachFragment (Android.Support.V4.App.Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is FilePickerFragment){
                FilePickerFragment = fragment as FilePickerFragment;
            }
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        #endregion

        #region Options Menu

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId){
            case Android.Resource.Id.Home:
                Finish ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        void TabSelected (object sender, TabLayout.TabSelectedEventArgs e)
        {
            if (e.Tab == NameTab){
                FilePickerFragment.SortByName ();
            }else if (e.Tab == DateTab){
                FilePickerFragment.SortByDate ();
            }else if (e.Tab == ContactTab){
                FilePickerFragment.SortByContact ();
            }
        }
    }
}
