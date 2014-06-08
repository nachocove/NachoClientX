//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Runtime.CompilerServices;
using System.IO;

namespace NachoCore.Utils
{
    public class NcAssert
    {
        // All subsequent assertion exceptions must be derived of NachoAssertionFailure.
        public class NachoAssertionFailure : Exception
        {
            public NachoAssertionFailure (string message, string sourceFile,
                int lineNumber, string member) : base (message)
            {
                string callerInfo = "";
                if ((0 < sourceFile.Length) && (0 < lineNumber)) {
                    callerInfo = String.Format("{0},{1}: ", Path.GetFileName(sourceFile), lineNumber);
                }
                if (0 < member.Length) {
                    callerInfo += member + "()";
                }
                if (0 < callerInfo.Length) {
                    callerInfo = " (" + callerInfo + ")";
                }
                Log.Error (Log.LOG_ASSERT, "{0}{1}", message, callerInfo);
            }
        }

        public class NachoDefaultCaseFailure : NachoAssertionFailure
        {
            public NachoDefaultCaseFailure (string message, string sourceFile,
                int lineNumber, string member) : base (message, sourceFile, lineNumber, member)
            {
            }
        }

        private NcAssert ()
        {
        }

        public static void True (bool b, string message = "",
            // You don't need to fill in these parameters, they will be
            // automatically assigned.
            [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string member = "")
        {
            if (!b) {
                throw new NachoAssertionFailure ("NcAssert.True: " + message, sourceFile, lineNumber, member);
            }
        }

        public static void NotNull (Object o, string message = "",
            // You don't need to fill in these parameters, they will be
            // automatically assigned.
            [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string member = "")
        {
            if (null == o) {
                throw new NachoAssertionFailure ("NcAssert.NotNull: " + message,
                    sourceFile, lineNumber, member);
            }
        }

        public static void CaseError (string message = "",
            // you don't need to fill in these parameters, they will be
            // automatically assigned.
            [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string member = "")
        {
            throw new NachoDefaultCaseFailure ("NcAssert.CaseError: " + message,
                sourceFile, lineNumber, member);
        }
    }
}

