// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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

        public Event[] Sequence { get; set; }

        public static Event Create (Event[] sequence)
        {
            return new Event () {
                EventCode = SmEvt.Sequence,
                Mnemonic = sequence [0].Mnemonic,
                Sequence = (Event[])sequence.Clone (),
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

        private string PseudoKlass;
        private Dictionary<string,uint> EventCode;
        private Dictionary<uint,string> EventName;
        private BlockingCollection<Event> EventQ;
        private Object LockObj;
        private bool InProcess;
        // Static.
        private static int NextId = 0;
        private static Object StaticLockObj = new Object ();
        private static ConcurrentDictionary<Type, Tuple<Dictionary<string,uint>, Dictionary<uint,string>>> CodeAndName =
            new ConcurrentDictionary<Type, Tuple<Dictionary<string,uint>, Dictionary<uint,string>>> ();
        private static ConcurrentDictionary<string, bool> IsValidated = new ConcurrentDictionary<string, bool> ();

        public NcStateMachine (string pseudoKlass)
        {
            lock (StaticLockObj) {
                Id = ++NextId;
            }
            PseudoKlass = pseudoKlass;
            EventQ = new BlockingCollection<Event> ();
            State = (uint)St.Start;
            LockObj = new Object ();
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

        /// <summary>
        /// Can be called from within an Action function to clear the event Q.
        /// Don't call if not in an action function - it won't be effective all the time.
        /// </summary>
        public void ClearEventQueue ()
        {
            Event dummy;
            while (EventQ.TryTake (out dummy)) {
                ;
            }
        }

        /// <summary>
        /// Posts at most one event (best effort - not a guarantee).
        /// </summary>
        /// <param name="eventCode">Event code.</param>
        /// <param name="mnemonic">Mnemonic.</param>
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

            lock (LockObj) {
                // Enter critical section.
                if ((uint)SmEvt.Sequence == smEvent.EventCode) {
                    NcAssert.True (null != smEvent.Sequence && 0 < smEvent.Sequence.Length);
                    foreach (var subSmEvent in smEvent.Sequence) {
                        EventQ.Add (subSmEvent);
                    }
                } else {
                    NcAssert.True (null == smEvent.Sequence);
                    EventQ.Add (smEvent);
                }
                if (InProcess) {
                    // If another thread is already working on this SM, then let it process
                    // the event we just added to the Q.
                    return;
                }
                // There isn't another thread already working, so we will be the working thread.
                InProcess = true;
                // Exit crticial section.
            }
            FireLoop ();
        }
        // It is critical that InProcess be false when we return from this function!
        private void FireLoop ()
        {
            while (true) {
                try {
                    var fireEvent = (Event)EventQ.Take ();
                    FireEventCode = fireEvent.EventCode;
                    Arg = fireEvent.Arg;
                    Message = fireEvent.Message;
                    if ((uint)St.Stop == State) {
                        Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => DROPPED IN St.Stop",
                            NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic), Message));
                        goto PossiblyLeave;
                    }
                    var hotNode = TransTable.Where (x => State == x.State).Single ();
                    if (null != hotNode.Drop && hotNode.Drop.Contains (FireEventCode)) {
                        Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => DROPPED EVENT",
                            NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic), Message));
                        goto PossiblyLeave;
                    }
                    if (null != hotNode.Invalid && hotNode.Invalid.Contains (FireEventCode)) {
                        Log.Error (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => INVALID EVENT",
                            NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic), Message));
                        goto PossiblyLeave;
                    }
                    var hotTrans = hotNode.On.Where (x => FireEventCode == x.Event).Single ();
                    Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => S={4}",
                        NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic, StateName (hotTrans.State)), Message));
                    Action = hotTrans.Act;
                    NextState = hotTrans.State;
                    Action ();
                    var oldState = State;
                    State = NextState;
                    if (oldState != State && null != StateChangeIndication) {
                        StateChangeIndication ();
                    }
                } catch (Exception ex) {
                    Log.Error (Log.LOG_STATE, "Exception in StateMachine.FireLoop: {0}", ex.ToString ());
                    lock (LockObj) {
                        InProcess = false;
                    }
                    throw ex;
                }
                PossiblyLeave:
                lock (LockObj) {
                    // Enter critical section.
                    if (0 == EventQ.Count) {
                        // If there is nothing to do, indicate we are leaving and leave.
                        InProcess = false;
                        return;
                    }
                    // Leave critical section.
                }
            }
        }

        public void Validate ()
        {
            if (IsValidated.ContainsKey (PseudoKlass)) {
                return;
            }
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
            IsValidated.TryAdd (PseudoKlass, true);
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
            // Note assigning to EventName is the last step!
            if (null != EventName) {
                return;
            }
            lock (StaticLockObj) {
                if (null == LocalEventType) {
                    LocalEventType = typeof(SmEvt);
                }
                Tuple<Dictionary<string,uint>, Dictionary<uint,string>> tup;
                if (CodeAndName.TryGetValue (LocalEventType, out tup)) {
                    EventCode = tup.Item1;
                    EventName = tup.Item2;
                    return;
                }
                var enumHolderType = LocalEventType;
                var eventCode = new Dictionary<string, uint> ();
                var eventName = new Dictionary<uint, string> ();

                while (typeof(System.Object) != enumHolderType) {
                    MemberInfo[] miArr = enumHolderType.GetMember ("E");

                    foreach (MemberInfo mi in miArr) {
                        var enumType = System.Type.GetType (mi.DeclaringType.FullName + "+" + mi.Name);
                        foreach (var enumMember in enumType.GetFields (BindingFlags.Public | BindingFlags.Static)) {
                            if (!"Last".Equals (enumMember.Name)) {
                                uint value = (uint)Convert.ChangeType (enumMember.GetValue (null), typeof(uint));
                                eventCode.Add (enumMember.Name, value);
                                eventName.Add (value, enumMember.Name);
                            }
                        }
                    }
                    enumHolderType = enumHolderType.BaseType;
                }

                CodeAndName.TryAdd (LocalEventType, Tuple.Create<Dictionary<string,uint>, Dictionary<uint,string>>
                    (eventCode, eventName));
                EventCode = eventCode;
                EventName = eventName;
            }
        }
    }
}
