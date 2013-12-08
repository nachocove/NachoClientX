using System;

namespace NachoCore.Utils
{
    public class NcResult
    {
        enum Kind { Uninitialized, OK, Info, Warning, Error };

        Kind kind;
        Object value;
        String message;

        NcResult ()
        {
            kind = Kind.Uninitialized;
            value = null;
            message = null;
        }
     
        public static NcResult OK()
        {
            NcResult r = new NcResult ();
            r.kind = Kind.OK;
            return r;
        }

        public static NcResult OK(Object o)
        {
            NcResult r = OK ();
            r.value = o;
            return r;
        }

        public static NcResult Error(String message)
        {
            NcResult r = new NcResult ();
            r.kind = Kind.Error;
            r.message = message;
            return r;
        }

        public bool isOK()
        {
            System.Diagnostics.Debug.Assert (Kind.Uninitialized != kind);
            return (Kind.OK == kind);
        }

        public bool isError()
        {
            System.Diagnostics.Debug.Assert (Kind.Uninitialized != kind);
            return (Kind.Error == kind);
        }

        public String GetString()
        {
            System.Diagnostics.Debug.Assert (Kind.Uninitialized != kind);
            return (String)value;
        }

        public Object GetObject()
        {
            System.Diagnostics.Debug.Assert (Kind.Uninitialized != kind);
            return value;
        }

        public int GetIndex()
        {
            System.Diagnostics.Debug.Assert (Kind.Uninitialized != kind);
            return System.Convert.ToInt32(value);
        }

        public String GetMessage()
        {
            System.Diagnostics.Debug.Assert (Kind.Uninitialized != kind);
            return message;
        }

    }
}

