//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

using MimeKit;

using NachoCore.Model;
using NachoCore.Utils;


namespace NachoCore.Brain
{
    /// <summary>
    /// A wrapped version of MimeMessage. This class allows MimeMessage to stream
    /// the content of its MIME parts on demand by holding a reference to the
    /// file stream that backs the MimeMessage object.
    /// </summary>
    public class NcMimeMessage : IDisposable
    {
        public MimeMessage Message;
        protected FileStream Stream;

        public NcMimeMessage ()
        {
        }


        public NcMimeMessage (string filePath, CancellationToken cToken)
        {
            Stream = new FileStream (filePath, FileMode.Open, FileAccess.Read);
            Message = MimeMessage.Load (Stream, true, cToken);
        }

        public void Dispose ()
        {
            Dispose (true);
        }

        protected void Dispose (bool isDisposing)
        {
            if (isDisposing) {
                Message = null;
                if (null != Stream) {
                    Stream.Dispose ();
                    Stream = null;
                }
            }
        }
    }

    /// <summary>
    /// We assume that each scorable object has a content stored in a file. So,
    /// paring each type of object involves getting a file path and returning 
    /// an object for each type. The caller knows the object type so it knows 
    /// which function to call.
    /// </summary>
    public class NcObjectParser
    {
        public static NcMimeMessage ParseMimeMessage (string path, CancellationToken cToken)
        {
            try {
                return new NcMimeMessage (path, cToken);
            } catch (Exception e) {
                Log.Error (Log.LOG_BRAIN, "fail to parse mime message (exception={0})", e);
                return null;
            }
        }

        public static string ParseFileMessage (string path)
        {
            try {
                return File.ReadAllText (path);
            } catch (Exception e) {
                Log.Error (Log.LOG_BRAIN, "fail to parse file message (exception={0})", e);
                return null;
            }
        }
    }
}

