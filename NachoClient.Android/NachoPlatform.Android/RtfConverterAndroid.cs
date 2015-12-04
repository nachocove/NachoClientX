//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoPlatform;
using System.IO;
using Com.Rtfparserkit.Converter.Text;
using Com.Rtfparserkit.Parser;

namespace NachoPlatform
{
    public class RtfConverter : IPlatformRtfConverter
    {
        public string ToHtml (string rtf)
        {
            var txt = ToTxt (rtf);
            var serializer = new HtmlTextDeserializer ();
            var doc = serializer.Deserialize (txt);
            var html = "";
            using (var writer = new StringWriter ()) {
                doc.Save (writer);
                html = writer.ToString ();
            }
            return html;
        }

        public string ToTxt (string rtf)
        {
            var converter = new StringTextConverter ();
            var source = new RtfStringSource (rtf);
            converter.Convert (source);
            return converter.Text;
        }
    }
}

