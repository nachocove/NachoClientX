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
using Android.Util;
using Android.Views;
using Android.Widget;

namespace NachoClient.AndroidClient
{
    public interface AttachmentPickerFragmentDelegate {
    }

    public class AttachmentPickerFragment : DialogFragment
    {

        GridView OptionsGridView;
        List<AttachmentOption> Options;

        public AttachmentPickerFragmentDelegate Delegate;

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var builder = new AlertDialog.Builder (Activity);
            var inflater = Activity.LayoutInflater;
            var view = inflater.Inflate (Resource.Layout.AttachmentPickerFragment, null);
            OptionsGridView = view.FindViewById<GridView> (Resource.Id.attachment_options);
            Options = new List<AttachmentOption> (new AttachmentOption[] {
                new AttachmentOption ("Add Photo", Resource.Drawable.calendar_add_photo, AddPhoto),
                new AttachmentOption ("Take Photo", Resource.Drawable.calendar_take_photo, TakePhoto),
                new AttachmentOption ("Add File", Resource.Drawable.calendar_add_files, AddFile)
            });
            OptionsGridView.Adapter = new AttachmentOptionsAdapter (this, Options);
            OptionsGridView.ItemClick += OptionClicked;
            builder.SetTitle ("Attach");
            builder.SetView (view);
            return builder.Create ();
        }

        public override void OnAttach (Activity activity)
        {
            base.OnAttach (activity);
        }

        void AddPhoto ()
        {
        }

        void TakePhoto ()
        {
        }

        void AddFile ()
        {
        }

        void OptionClicked (object sender, AdapterView.ItemClickEventArgs e)
        {
            var option = Options [e.Position];
            option.Action ();
        }
    }

    public class AttachmentOption
    {
        public string Label;
        public int Drawable;
        public Action Action;

        public AttachmentOption (string label, int drawable, Action action)
        {
            Label = label;
            Drawable = drawable;
            Action = action;
        }
    }

    public class AttachmentOptionsAdapter : BaseAdapter<AttachmentOption>
    {

        Fragment Parent;
        List<AttachmentOption> Options;

        public AttachmentOptionsAdapter (Fragment parent, List<AttachmentOption> options)
        {
            Parent = parent;
            Options = options;
        }

        public override int Count {
            get {
                return Options.Count;
            }
        }

        public override AttachmentOption this[int index] {
            get {
                return Options [index];
            }
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            var view = convertView;
            if (view == null) {
                view = Parent.Activity.LayoutInflater.Inflate (Resource.Layout.AttachmentOptionItemView, null);
            }
            var option = Options [position];
            var icon = view.FindViewById<ImageView> (Resource.Id.option_icon);
            var label = view.FindViewById<TextView> (Resource.Id.option_label);
            icon.SetImageResource (option.Drawable);
            label.Text = option.Label;
            return view;
        }

    }
}

