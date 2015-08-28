//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace NachoCore.Utils
{
    public delegate void TestModeAction (params string[] parameters);
        
    public class TestMode
    {
        // All test mode commands must be prefix by this pattern
        private string Prefix = "###";

        private static volatile object LockObj = new object ();
        private static TestMode _Instance;

        protected ConcurrentDictionary<string, TestModeAction> CommandTable;

        public static TestMode Instance {
            get {
                if (null == _Instance) {
                    lock (LockObj) {
                        if (null == _Instance) {
                            _Instance = new TestMode ();
                        }
                    }
                }
                return _Instance;
            }
        }

        public TestMode ()
        {
            CommandTable = new ConcurrentDictionary<string, TestModeAction> ();
        }

        protected bool IsTestModeCommand (string cmd)
        {
            return cmd.StartsWith (Prefix);
        }

        public bool Add (string cmd, TestModeAction action)
        {
            return CommandTable.TryAdd (cmd, action);
        }

        public bool Process (string cmd)
        {
            if (!IsTestModeCommand (cmd)) {
                return false;
            }

            var parameters = cmd.Split (new char[] { ' ' }, 50);
            TestModeAction action;
            if (!CommandTable.TryGetValue (parameters [0].Substring (Prefix.Length), out action)) {
                return false;
            }

            action (parameters.Skip (1).ToArray ());
            return true;
        }
    }
}

