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

namespace NachoClient.AndroidClient
{
    [Activity (Label = "NoteActivity")]            
    public class NoteActivity : Activity
    {
        private const string EXTRA_NOTE_TITLE = "com.nachocove.nachomail.EXTRA_NOTE_TITLE";
        private const string EXTRA_NOTE_TEXT = "com.nachocove.nachomail.EXTRA_NOTE_TEXT";
        private const string EXTRA_NOTE_ADD_DATE = "com.nachocove.nachomail.EXTRA_NOTE_ADD_DATE";

        private TextView title;
        private TextView doneButton;
        private EditText textField;
        private string unmodifiedText;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.NoteActivity);

            title = FindViewById<TextView> (Resource.Id.title);
            title.SetSingleLine ();
            title.Visibility = ViewStates.Visible;

            doneButton = FindViewById<TextView> (Resource.Id.right_text_button1);
            doneButton.Visibility = ViewStates.Visible;
            doneButton.Text = "Done";
            doneButton.Click += DoneButton_Click;

            textField = FindViewById<EditText> (Resource.Id.note_text);

            string titleText;
            if (Intent.HasExtra (EXTRA_NOTE_TITLE)) {
                titleText = string.Format ("Note: {0}", Intent.GetStringExtra (EXTRA_NOTE_TITLE));
            } else {
                titleText = "Note";
            }
            if (28 < titleText.Length) {
                titleText = titleText.Substring (0, 27) + "...";
            }
            title.Text = titleText;

            if (Intent.HasExtra (EXTRA_NOTE_TEXT)) {
                unmodifiedText = Intent.GetStringExtra (EXTRA_NOTE_TEXT);
            } else {
                unmodifiedText = "";
            }

            if (Intent.GetBooleanExtra (EXTRA_NOTE_ADD_DATE, false)) {
                unmodifiedText = DateTime.Now.ToShortDateString () + "\n\n\n" + unmodifiedText;
            }

            textField.Text = unmodifiedText;
        }

        public static Intent EditNoteIntent (Context context, string title, string text, bool insertDate)
        {
            var intent = new Intent (context, typeof(NoteActivity));
            intent.SetAction (Intent.ActionInsertOrEdit);
            if (!string.IsNullOrEmpty(title)) {
                intent.PutExtra (EXTRA_NOTE_TITLE, title);
            }
            if (null != text) {
                intent.PutExtra (EXTRA_NOTE_TEXT, text);
            }
            if (insertDate) {
                intent.PutExtra (EXTRA_NOTE_ADD_DATE, insertDate);
            }
            return intent;
        }

        public static string ModifiedNoteText (Intent resultIntent)
        {
            if (resultIntent == null || !resultIntent.HasExtra (EXTRA_NOTE_TEXT)) {
                return null;
            }
            return resultIntent.GetStringExtra (EXTRA_NOTE_TEXT);
        }

        private void DoneButton_Click (object sender, EventArgs e)
        {
            if (textField.Text == unmodifiedText) {
                SetResult (Result.Canceled);
            } else {
                var resultIntent = new Intent ();
                resultIntent.PutExtra (EXTRA_NOTE_TEXT, textField.Text);
                SetResult (Result.Ok, resultIntent);
            }
            Finish ();
        }
    }
}

