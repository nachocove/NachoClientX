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
using NachoCore;
using Java.Net;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "Nacho Mail")]
    [IntentFilter (new[] { Intent.ActionSend, Intent.ActionSendMultiple }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "*/*")]
    [IntentFilter (new[] { Intent.ActionSendto }, Categories = new[] { Intent.CategoryDefault }, DataScheme = "mailto", DataMimeType = "*/*")]
    public class MessageComposePublicListener : NcActivity
    {
        protected override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.WaitingFragment);
            FindViewById<TextView> (Resource.Id.textview).Text = "Loading Nacho Mail";

            // Use a C# task instead of an NcTask, because the app might not be initialized yet.
            System.Threading.Tasks.Task.Run (() => {

                MainApplication.OneTimeStartup ("MessageComposePublicListener");

                var message = new McEmailMessage ();
                message.AccountId = NcApplication.Instance.DefaultEmailAccount.Id;

                string initialText = "";

                if (Intent.HasExtra (Intent.ExtraEmail)) {
                    message.To = string.Join (", ", Intent.GetStringArrayExtra (Intent.ExtraEmail));
                }
                if (Intent.HasExtra (Intent.ExtraCc)) {
                    message.Cc = string.Join (", ", Intent.GetStringArrayExtra (Intent.ExtraCc));
                }
                if (Intent.HasExtra (Intent.ExtraBcc)) {
                    message.Bcc = string.Join (", ", Intent.GetStringArrayExtra (Intent.ExtraBcc));
                }
                if (Intent.HasExtra (Intent.ExtraSubject)) {
                    message.Subject = Intent.GetStringExtra (Intent.ExtraSubject);
                }
                if (Intent.HasExtra (Intent.ExtraText)) {
                    initialText = Intent.GetStringExtra (Intent.ExtraText);
                }

                var attachments = new List<McAttachment> ();
                if (Intent.HasExtra (Intent.ExtraStream)) {
                    try {
                        if (Intent.ActionSendMultiple == Intent.Action) {
                            var uris = Intent.GetParcelableArrayListExtra (Intent.ExtraStream);
                            foreach (var uriObject in uris) {
                                var attachment = AttachmentHelper.UriToAttachment (message.AccountId, this, (Android.Net.Uri)uriObject, Intent.Type);
                                if (null != attachment) {
                                    attachments.Add (attachment);
                                }
                            }
                        } else {
                            var uri = (Android.Net.Uri)Intent.GetParcelableExtra (Intent.ExtraStream);
                            var attachment = AttachmentHelper.UriToAttachment (message.AccountId, this, uri, Intent.Type);
                            if (null != attachment) {
                                attachments.Add (attachment);
                            }
                        }
                    } catch (Exception e) {
                        Log.Error (Log.LOG_LIFECYCLE, "Exception while processing the STREAM extra of a Send intent: {0}", e.ToString ());
                    }
                }

                Intent composeIntent;
                if (0 < attachments.Count) {
                    composeIntent = MessageComposeActivity.MessageWithAttachmentsIntent (this, message, initialText, attachments);
                } else {
                    composeIntent = MessageComposeActivity.InitialTextIntent (this, message, initialText);
                }

                RunOnUiThread (() => {
                    StartActivity (composeIntent);
                    Finish ();
                });
            });
        }

    }
}

