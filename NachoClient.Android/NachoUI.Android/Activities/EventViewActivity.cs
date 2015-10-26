//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
using Android.Widget;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "EventViewActivity")]            
    public class EventViewActivity : NcActivity, IEventViewFragmentOwner
    {
        private const string EXTRA_EVENT = "com.nachocove.nachomail.EXTRA_EVENT";

        private McEvent ev;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            this.ev = IntentHelper.RetreiveValue<McEvent> (Intent.GetStringExtra (EXTRA_EVENT));
            SetContentView (Resource.Layout.EventViewActivity);
        }

        McEvent IEventViewFragmentOwner.EventToView {
            get {
                return this.ev;
            }
        }

        public static Intent ShowEventIntent (Context context, McEvent ev)
        {
            var intent = new Intent (context, typeof(EventViewActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_EVENT, IntentHelper.StoreValue (ev));
            return intent;
        }
    }
}

