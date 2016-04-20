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
    public class NoteActivity : NcActivity
    {
        private const string EXTRA_NOTE_TITLE = "com.nachocove.nachomail.EXTRA_NOTE_TITLE";
        private const string EXTRA_NOTE_TEXT = "com.nachocove.nachomail.EXTRA_NOTE_TEXT";
        private const string EXTRA_NOTE_ADD_DATE = "com.nachocove.nachomail.EXTRA_NOTE_ADD_DATE";
        private const string EXTRA_NOTE_INSTRUCTIONS = "com.nachocove.nachomail.EXTRA_NOTE_INSTRUCTIONS";

        private ButtonBar buttonBar;
        private TextView instructions;
        private EditText textField;
        private string unmodifiedText;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.NoteActivity);

            buttonBar = new ButtonBar (FindViewById<View> (Resource.Id.button_bar));

            buttonBar.SetTextButton (ButtonBar.Button.Right1, "Done", DoneButton_Click);

            buttonBar.SetIconButton (ButtonBar.Button.Left1, Resource.Drawable.gen_close, CancelButton_Click);

            instructions = FindViewById<TextView> (Resource.Id.note_instructions);

            textField = FindViewById<EditText> (Resource.Id.note_text);

            if (Intent.HasExtra (EXTRA_NOTE_TITLE)) {
                buttonBar.SetTitle (Intent.GetStringExtra (EXTRA_NOTE_TITLE));
            }

            if (Intent.HasExtra (EXTRA_NOTE_INSTRUCTIONS)) {
                instructions.Text = Intent.GetStringExtra (EXTRA_NOTE_INSTRUCTIONS);
                instructions.Visibility = ViewStates.Visible;
            } else {
                instructions.Visibility = ViewStates.Gone;
            }

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

        public override void OnBackPressed ()
        {
            SaveChanges ();
        }

        private void SaveChanges ()
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

        public static Intent EditNoteIntent (Context context, string title, string instructions, string text, bool insertDate)
        {
            var intent = new Intent (context, typeof(NoteActivity));
            intent.SetAction (Intent.ActionInsertOrEdit);
            if (!string.IsNullOrEmpty (title)) {
                intent.PutExtra (EXTRA_NOTE_TITLE, title);
            }
            if (null != text) {
                intent.PutExtra (EXTRA_NOTE_TEXT, text);
            }
            if (insertDate) {
                intent.PutExtra (EXTRA_NOTE_ADD_DATE, insertDate);
            }
            if (null != instructions) {
                intent.PutExtra (EXTRA_NOTE_INSTRUCTIONS, instructions);
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
            SaveChanges ();
        }

        private void CancelButton_Click (object sender, EventArgs e)
        {
            SetResult (Result.Canceled);
            Finish ();
        }
    }
}

