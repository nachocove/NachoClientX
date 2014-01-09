using System;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class NcResult
    {
        // Eventually match up with Syslog severity levels for non-OK results.
        // https://en.wikipedia.org/wiki/Syslog.
        public enum Kind { OK, Info, Warning, Error };

        Kind kind;
        Object value;
        String message;

        private NcResult ()
        {
            kind = Kind.Error;
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
            return (Kind.OK == kind);
        }

        public bool isError()
        {
            return (Kind.Error == kind);
        }

        public T GetValue<T>()
        {
            return (T) value;
        }

        public String GetMessage()
        {
            return message;
        }

    }
}

