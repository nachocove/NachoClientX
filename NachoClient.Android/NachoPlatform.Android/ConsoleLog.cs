//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;

namespace NachoPlatform
{
    public class ConsoleLog : IConsoleLog
    {
        private string Tag;

        public static IConsoleLog Create(string subsystem)
        {
            return new ConsoleLog (subsystem);
        }

        public ConsoleLog (string subsystem)
        {
            Tag = NachoClient.Build.BuildInfo.PackageName + "." + subsystem;
        }

        public void Debug (string message, params object [] args)
        {
            Android.Util.Log.Debug (Tag, string.Format (Log.DefaultFormatter, message, args));
        }

        public void Info (string message, params object [] args)
        {
            Android.Util.Log.Info (Tag, string.Format (Log.DefaultFormatter, message, args));
        }

        public void Warn (string message, params object [] args)
        {
            Android.Util.Log.Warn (Tag, string.Format (Log.DefaultFormatter, message, args));
        }

        public void Error (string message, params object [] args)
        {
            Android.Util.Log.Error (Tag, string.Format (Log.DefaultFormatter, message, args));
        }
    }
}
