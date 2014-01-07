using System;
using NachoCore.Model;

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
            NachoCore.NachoAssert.True (Kind.Uninitialized != kind);
            return (Kind.OK == kind);
        }

        public bool isError()
        {
            NachoCore.NachoAssert.True (Kind.Uninitialized != kind);
            return (Kind.Error == kind);
        }

        public T GetValue<T>()
        {
            NachoCore.NachoAssert.True (Kind.Uninitialized != kind);
            return (T) value;
        }

        public String GetMessage()
        {
            NachoCore.NachoAssert.True (Kind.Uninitialized != kind);
            return message;
        }

    }
}

