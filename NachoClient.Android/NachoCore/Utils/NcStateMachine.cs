// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NachoCore.Utils
{
    // Model of use is that it is instantiated with an
    // "owner" (aka delegate in the Obj-C sense of the word).
    // Signature of event handling callback.
    public delegate void Cb ();
    // This is the list of must-support events (not-ActiveSync specific).
    // Each user starts creating event codes at Last+1.
    // Types of failure:
    // Hard - there is something about the interaction that just won't work.
    // Temp - the cause of the failure is expected to clear with time (i.e. network failure).
    // If we can't communicate with the server - we assume that's a TempFail.
    // Precise events are used to indicate value-based failures (e.g. credential, server config, etc).
    public class SmEvt
    {
        // Sequence is a place-holder for a sequence of Events posted at once. Not a "real" event code.
        public const uint Sequence = 1000000;

        public enum E : uint
        {
            Launch,
            Success,
            HardFail,
            TempFail,
            Last = TempFail,
        };
    }
    // { state => { event => [handlers, ...]}}.
    // All possible events must be covered.
    // 1st handler in list is the event hander (required).
    public enum St : uint
    {
        Start,
        Stop,
        Last = Stop,
    };

    public class Node
    {
        public uint State { set; get; }

        public Trans[] On { set; get; }

        public uint[] Drop { set; get; }

        public uint[] Invalid { set; get; }
    }

    public class Trans
    {
        public uint State { set; get; }

        public Cb Act { set; get; }

        public uint Event { set; get; }
    }

    public class Event
    {
        public uint EventCode { get; set; }

        public string Mnemonic { get; set; }

        public object Arg { get; set; }

        public string Message { get; set; }

        public bool DropIfStopped { get; set; }

        public Event[] Sequence { get; set; }

        public static Event Create (Event[] sequence)
        {
            return new Event () {
                EventCode = SmEvt.Sequence,
                Mnemonic = sequence[0].Mnemonic,
                Sequence = (Event[])sequence.Clone(),
            };
        }

        public static Event Create (uint eventCode, string mnemonic)
        {
            return new Event () { EventCode = eventCode, Mnemonic = mnemonic };
        }

        public static Event Create (uint eventCode, string mnemonic, object arg)
        {
            return new Event () { EventCode = eventCode, Mnemonic = mnemonic, Arg = arg };
        }

        public static Event Create (uint eventCode, string mnemonic, object arg, string message)
        {
            return new Event () { EventCode = eventCode, Mnemonic = mnemonic, Arg = arg, Message = message };
        }
    }

    public sealed class NcStateMachine
    {
        public string Name { set; get; }

        public int Id { set; get; }

        public Type LocalEventType { set; get; }

        public Type LocalStateType { set; get; }

        public Node[] TransTable { set; get; }

        public uint State { set; get; }

        public uint NextState { set; get; }

        public uint FireEventCode { set; get; }

        public Cb Action { set; get; }

        public object Arg { set; get; }

        public string Message { set; get; }

        public Cb StateChangeIndication { set; get; }

        private Dictionary<string,uint> EventCode;
        private Dictionary<uint,string> EventName;
        private Queue EventQ;
        private bool IsFiring;
        private static int NextId = 0;
        private static Object NextIdLockObj = new Object ();

        public NcStateMachine ()
        {
            lock (NextIdLockObj) {
                Id = ++NextId;
            }
            EventQ = new Queue ();
            State = (uint)St.Start;
        }

        public string NameAndId ()
        {
            return string.Format ("({0}:{1})", Name, Id);
        }

        public void Start ()
        {
            Start ((uint)St.Start);
        }

        public void Start (uint StartState)
        {
            State = StartState;
            PostEvent ((uint)SmEvt.E.Launch, "SMSTART");
        }

        public void PostAtMostOneEvent (uint eventCode, string mnemonic)
        {
            foreach (var elem in EventQ) {
                var inQEvent = (Event)elem;
                if (eventCode == inQEvent.EventCode) {
                    Log.Info (Log.LOG_STATE, "SM{0}: E={1} already in queue.", NameAndId (), EventName [eventCode]);
                    return;
                }
            }
            PostEvent (eventCode, mnemonic);
        }

        public void PostEvent (uint eventCode, string mnemonic)
        {
            PostEvent (eventCode, mnemonic, null, null);
        }

        public void PostEvent (uint eventCode, string mnemonic, object arg, string message)
        {
            PostEvent (Event.Create (eventCode, mnemonic, arg, message));
        }

        public void PostEvent (Event smEvent)
        {
            BuildEventDicts ();

            if ((uint)SmEvt.Sequence == smEvent.EventCode) {
                NachoAssert.True (null != smEvent.Sequence && 0 < smEvent.Sequence.Length);
                foreach (var subSmEvent in smEvent.Sequence) {
                    EventQ.Enqueue (subSmEvent);
                }
            } else {
                NachoAssert.True (null == smEvent.Sequence);
                EventQ.Enqueue (smEvent);
            }
            if (IsFiring) {
                return;
            }
            IsFiring = true;
            while (0 != EventQ.Count) {
                var fireEvent = (Event)EventQ.Dequeue ();
                FireEventCode = fireEvent.EventCode;
                Arg = fireEvent.Arg;
                Message = fireEvent.Message;
                if ((uint)St.Stop == State) {
                    if (fireEvent.DropIfStopped) {
                        Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => DROPPED IN St.Stop",
                            NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic), Message));
                        continue;
                    } else {
                        Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => EVENT WHILE IN St.Stop",
                            NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic), Message));
                        throw new Exception ();
                    }
                }
                var hotNode = TransTable.Where (x => State == x.State).Single ();
                if (null != hotNode.Drop && hotNode.Drop.Contains (FireEventCode)) {
                    Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => DROPPED EVENT",
                        NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic), Message));
                    continue;
                }
                if (null != hotNode.Invalid && hotNode.Invalid.Contains (FireEventCode)) {
                    Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => INVALID EVENT",
                        NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic), Message));
                    throw new Exception ();
                }
                var hotTrans = hotNode.On.Where (x => FireEventCode == x.Event).Single ();
                Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => S={4}",
                    NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic, StateName (hotTrans.State)), Message));
                Action = hotTrans.Act;
                NextState = hotTrans.State;
                Action ();
                State = NextState;
                if (null != StateChangeIndication) {
                    StateChangeIndication ();
                }
            }
            IsFiring = false;
        }

        public void Validate ()
        {
            BuildEventDicts ();

            var errors = new List<string> ();
            foreach (var stateNode in TransTable) {
                foreach (var nameNCode in EventCode) {
                    var eventCode = nameNCode.Value;
                    var forEventCode = stateNode.On.Where (x => eventCode == x.Event).ToList ();
                    switch (forEventCode.Count) {
                    case 1:
                        // Ensure not in Drop nor Invalid.
                        if (null != stateNode.Drop && Array.Exists (stateNode.Drop, y => eventCode == y)) {
                            errors.Add (string.Format ("State {0}, event code {1} exists both in Drop and Node.", 
                                StateName (stateNode.State),
                                EventName [eventCode]));
                        }
                        if (null != stateNode.Invalid && Array.Exists (stateNode.Invalid, y => eventCode == y)) {
                            errors.Add (string.Format ("State {0}, event code {1} exists both in Invalid and Node.", 
                                StateName (stateNode.State),
                                EventName [eventCode]));
                        }
                        break;
                    case 0:
                        bool inDrop = false, inInvalid = false;
                        if (null != stateNode.Drop) {
                            inDrop = Array.Exists (stateNode.Drop, y => eventCode == y);
                        }
                        if (null != stateNode.Invalid) {
                            inInvalid = Array.Exists (stateNode.Invalid, y => eventCode == y);
                        }
                        // Make sure in either Drop or Invalid, but not both.
                        if (inDrop && inInvalid) {
                            errors.Add (string.Format ("State {0}, event code {1} exists both in Invalid and Drop.",
                                StateName (stateNode.State),
                                EventName [eventCode]));
                        } else if (!inDrop && !inInvalid) {
                            errors.Add (string.Format ("State {0}, event code {1} exists in none of Node, Drop nor Invalid.",
                                StateName (stateNode.State),
                                EventName [eventCode]));
                        }
                        break;
                    default:
                        // Event is in Node multiple times.
                        errors.Add (string.Format ("State {0}, event code {1} exists in multiple Trans in same Node.",
                            StateName (stateNode.State),
                            EventName [eventCode]));
                        break;
                    }
                }
            }
            foreach (var error in errors) {
                Log.Error (Log.LOG_STATE, error);
            }
            if (0 != errors.Count) {
                throw new Exception (string.Format ("State machine {0} needs to be rectified.", Name));
            }
        }

        private string LogLine (string preString, string message)
        {
            if (null != message) {
                return string.Format ("{0}: {1}", preString, message);
            }
            return preString;
        }

        private string StateName (uint state)
        {
            if ((uint)St.Last < (uint)state) {
                if (null != LocalStateType) {
                    return Enum.GetName (LocalStateType, state);
                }
                return state.ToString ();
            }
            return Enum.GetName (typeof(St), state);
        }

        private void BuildEventDicts ()
        {
            if (null != EventCode) {
                return;
            }
            // NOTE: these could be cached based on the LocalEventType, rather than rebuilding for every instance.
            EventCode = new Dictionary<string, uint> ();
            EventName = new Dictionary<uint, string> ();
            if (null == LocalEventType) {
                LocalEventType = typeof(SmEvt);
            }
            var enumHolderType = LocalEventType;

            while (typeof(System.Object) != enumHolderType) {
                MemberInfo[] miArr = enumHolderType.GetMember ("E");

                foreach (MemberInfo mi in miArr) {
                    var enumType = System.Type.GetType (mi.DeclaringType.FullName + "+" + mi.Name);
                    foreach (var enumMember in enumType.GetFields (BindingFlags.Public | BindingFlags.Static)) {
                        if (!"Last".Equals (enumMember.Name)) {
                            uint value = (uint)Convert.ChangeType (enumMember.GetValue (null), typeof(uint));
                            EventCode.Add (enumMember.Name, value);
                            EventName.Add (value, enumMember.Name);
                        }
                    }
                }
                enumHolderType = enumHolderType.BaseType;
            }
        }
    }
}
