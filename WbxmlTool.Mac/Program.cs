//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using NachoCore.Wbxml;

namespace WbxmlTool.Mac
{
    enum OperationType {
        NONE = 0,
        ENCODE,
        DECODE,
    }

    class MainClass
    {
        static string Base64;

        static string Xml;

        static OperationType Operation;

        static string Filename;

        private static void Error(string msg)
        {
            Console.WriteLine (msg);
            Environment.Exit (1);
        }

        private static void ParseCommand (string[] args)
        {
            if (0 == args.Length) {
                Usage ();
            }
            for (int n = 0; n < args.Length; n++) {
                switch (args [n]) {
                case "-b":
                    n++;
                    if (args.Length <= n) {
                        Error ("No base64 string given.");
                    }
                    Base64 = args [n];
                    break;
                case "-d":
                    Operation = OperationType.DECODE;
                    break;
                case "-e":
                    Operation = OperationType.ENCODE;
                    break;
                case "-x":
                    n++;
                    if (args.Length <= n) {
                        Error ("No XML string given.");
                    }
                    Xml = args [n];
                    break;
                case "-f":
                    n++;
                    if (args.Length <= n) {
                        Error ("No file name given.");
                    }
                    Filename = args[n];
                    break;
                case "-h":
                    Usage ();
                    break;
                }
            }
        }

        private static Stream OpenInputStream ()
        {
            Stream stream = null;
            if (null == Filename) {
                return null;
            }
            try {
                if ("-" == Filename) {
                    stream = Console.OpenStandardInput ();
                } else {
                    stream = new FileStream (Filename, FileMode.Open, FileAccess.Read);
                }
            }
            catch (System.UnauthorizedAccessException e) {
                Error (e.Message);
            }
            catch (System.IO.IOException e) {
                Error (e.Message);
            }
            return stream;
        }

        private static void Encode ()
        {
            ASWBXML wbxml = new ASWBXML (new CancellationToken());
            try {
                if (null != Xml) {
                    wbxml.XmlDoc = XDocument.Parse (Xml);
                } else if (null != Filename) {
                    wbxml.XmlDoc = XDocument.Load (OpenInputStream ());
                } else {
                    Error ("No file or XML string given.");
                }
            }
            catch (System.Xml.XmlException e) {
                Error (String.Format("XML error ({0})", e.Message));
            }
            byte[] data = wbxml.GetBytes (false);
            Console.WriteLine ("{0}", Convert.ToBase64String (data));
        }

        private static void Decode ()
        {
            ASWBXML wbxml = new ASWBXML (new CancellationToken ());
            if (null == Base64) {
                if (null != Filename) {
                    using (Stream s = OpenInputStream ())
                    using (StreamReader sr = new StreamReader (s)) {
                        Base64 = sr.ReadToEnd ();
                    }
                } else {
                    Error ("No file or Base64 string given.");
                }
            }

            if (null != Base64) {
                try {
                    byte[] data = Convert.FromBase64String (Base64);
                    MemoryStream memStream = new MemoryStream (data);
                    wbxml.LoadBytes (memStream, false);
                }
                catch (System.FormatException e) {
                    Error (String.Format("Invalid base64 data ({0})", e.Message));
                }
            } else {
                Error ("Base64 string is null");
            }

            Console.WriteLine ("{0}", wbxml.XmlDoc);
        }

        public static void Usage ()
        {
            Console.WriteLine ("Usage: mono WbxmlTool.Mac.exe [-d | -e] [-b base64 | -x xml | -f filename]\n\n" +
            "-d            Decode WBXML to XML\n" +
            "-e            Encode XML to WBXML\n" +
            "-b base64     A base64 encoded WBXML\n" +
            "-x xml        A (ActiveSync) XML\n" +
            "-f filename   File name of a file that has either a base64 or a XML string\n" +
            "-h            Print this usage.");
            Environment.Exit (0);
        }

        public static void Main (string[] args)
        {
            Operation = OperationType.NONE;
            ParseCommand (args);
            switch (Operation) {
            case OperationType.ENCODE:
                Encode ();
                break;
            case OperationType.DECODE:
                Decode ();
                break;
            default:
                Error ("no operation specified");
                break;
            }
        }
    }
}
