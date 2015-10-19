
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace NachoClient.AndroidClient
{

    public interface IntentFragmentDelegate {
        void IntentFragmentDidSelectIntent ();
    }

    public class IntentFragment : DialogFragment
    {

        IntentFragmentDelegate Delegate;

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            var inflater = Activity.LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.IntentFragment, null);
            builder.SetTitle ("Select Intent");
            builder.SetView (view);
            return builder.Create ();
        }

        public override void OnAttach (Activity activity)
        {
            base.OnAttach (activity);
            Delegate = activity as IntentFragmentDelegate;
        }

    }
}
