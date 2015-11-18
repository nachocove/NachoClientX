//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoPlatform;
using Foundation;

namespace NachoPlatform
{
    public class RtfConverter : IPlatformRtfConverter
    {
        public RtfConverter ()
        {
        }

        public string ToHtml (string rtf)
        {
            var str = AttributedStringFromRtf (rtf);
            NSError error = null;
            NSData htmlData = str.GetDataFromRange (
                                  new NSRange (0, str.Length),
                                  new NSAttributedStringDocumentAttributes { DocumentType = NSDocumentType.HTML },
                                  ref error);
            return htmlData.ToString ();
        }

        public string ToTxt (string rtf)
        {
            var str = AttributedStringFromRtf (rtf);
            return str.Value;
        }

        private NSAttributedString AttributedStringFromRtf (string rtf)
        {
            var data = NSData.FromString (rtf);
            NSError error = null;
            var str = new NSAttributedString (
                          data,
                          new NSAttributedStringDocumentAttributes { DocumentType = NSDocumentType.RTF },
                          ref error);
            return str;
        }
    }
}

