//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore.ActiveSync;
using System.Threading;


namespace Test.iOS
{
    [TestFixture]
    public class AsNcStateMachineTest : BaseNcStateMachineTest
    {
        private static bool isBeingUsed;
        public NcStateMachine OwnerSm;

        [Test]
        public void TestStateMachine ()
        {
            isBeingUsed = false;

            var autoResetEvent = new AutoResetEvent(false);
            OwnerSm = CreatePhonySM (() => {
                autoResetEvent.Set ();
            });
            OwnerSm.Start ();
            OwnerSm.PostEvent ((uint)SmEvt.E.Launch, "Launch-State1");

            bool didFinish = autoResetEvent.WaitOne (2000);
            Assert.IsTrue (didFinish, "Operation did not finish");
        }

        private void FirstAction ()
        {
            isBeingUsed = true;
            OwnerSm.PostEvent ((uint)SmEvt.E.Launch, "Launch-State2");
            isBeingUsed = false;
        }

        private void SecondAction ()
        {
            Assert.IsTrue (isBeingUsed != true, "SecondAction should not be processed until first action is complete");
            OwnerSm.PostEvent ((uint)SmEvt.E.Success, "Stop-Machine");
        }

        private NcStateMachine CreatePhonySM (Action action)
        {
            var sm = new NcStateMachine ("PHONY") {
                Name = "SmTestMachine",
                LocalEventType = typeof(AsProtoControl.CtlEvt),
                LocalStateType = typeof(PhonySt),
                TransTable = new [] {
                    new Node {State = (uint)St.Start,
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = delegate () {},
                                State = (uint)PhonySt.State1 },
                        }
                    },
                    new Node {State = (uint)PhonySt.State1,
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = delegate () {
                                    FirstAction ();
                                },
                                State = (uint)PhonySt.State2 },
                        }
                    },
                    new Node {State = (uint)PhonySt.State2,
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch, 
                                Act = delegate () {
                                    SecondAction ();
                                },
                                State = (uint)PhonySt.FinishedState },
                        }
                    },
                    new Node {State = (uint)PhonySt.FinishedState,
                        On = new [] {
                            new Trans {
                                Event = (uint)SmEvt.E.Success, 
                                Act = delegate () {
                                    Log.Info (Log.LOG_TEST, "Reached stop state; shutting down machine");
                                    action ();
                                },
                                State = (uint)PhonySt.State1 },
                        }
                    },
                }
            };
            return sm;
        }
    }

    public class BaseNcStateMachineTest : CommonTestOps
    {
        [SetUp]
        public new void SetUp ()
        {
            base.SetUp ();
        }

        public enum PhonySt : uint
        {
            State1 = (AsProtoControl.Lst.QOpW + 1),
            State2,
            FinishedState,
            Last = State2,
        };
    }
}

