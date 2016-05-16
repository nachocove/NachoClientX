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

        public Action Act { set; get; }

        public uint Event { set; get; }

        public bool ActSetsState { set; get; }
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

        public Action Action { set; get; }

        public object Arg { set; get; }

        public object Context { private set; get; }

        public string Message { set; get; }

        public Action StateChangeIndication { set; get; }

        public Action TransIndication { set; get; }

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

        public NcStateMachine (string pseudoKlass, object context = null)
        {
            lock (StaticLockObj) {
                Id = ++NextId;
            }
            PseudoKlass = pseudoKlass;
            EventQ = new BlockingCollection<Event> ();
            State = (uint)St.Start;
            LockObj = new Object ();
            Context = context;
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

        public int EventQueueDepth ()
        {
            return EventQ.Count;
        }

        public void PostEvent (uint eventCode, string mnemonic)
        {
            PostEvent (eventCode, mnemonic, null, null);
        }

        public void PostEvent (uint eventCode, string mnemonic, object arg)
        {
            PostEvent (Event.Create (eventCode, mnemonic, arg, null));
        }

        public void PostEvent (uint eventCode, string mnemonic, object arg, string message)
        {
            PostEvent (Event.Create (eventCode, mnemonic, arg, message));
        }

        public void PostEvent (Event smEvent)
        {
            BuildEventDicts ();
            bool addFailed = false;
            lock (LockObj) {
                // Enter critical section.
                if ((uint)SmEvt.Sequence == smEvent.EventCode) {
                    NcAssert.True (null != smEvent.Sequence && 0 < smEvent.Sequence.Length);
                    foreach (var subSmEvent in smEvent.Sequence) {
                        EventQ.Add (subSmEvent);
                    }
                } else {
                    NcAssert.True (null == smEvent.Sequence);
                    if (!EventQ.TryAdd (smEvent)) {
                        addFailed = true;
                    }
                }
                if (addFailed) {
                    Log.Error (Log.LOG_STATE, "EventQ.TryAdd failed");
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
                    var hotNode = TransTable.Where (x => State == x.State).SingleOrDefault ();
                    if (null == hotNode) {
                        Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => INVALID TRANSITION",
                            NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic), Message));
                        NcAssert.True (false);
                    }
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
                    var hotTrans = hotNode.On.Where (x => FireEventCode == x.Event).SingleOrDefault ();
                    if (null == hotTrans) {
                        Log.Error (Log.LOG_STATE, "No transition for SM {0} on {1}", Name, FireEventCode);
                        NcAssert.True (false);
                    }
                    string stateName = !hotTrans.ActSetsState ? StateName (hotTrans.State) : string.Format ("(TBD {0}.{1}())", hotTrans.Act.Target.GetType ().Name, hotTrans.Act.Method.Name);
                    Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => S={4}",
                        NameAndId (), StateName (State), EventName [FireEventCode], fireEvent.Mnemonic, stateName), Message));
                    Action = hotTrans.Act;
                    NextState = hotTrans.State;
                    var oldState = State;
                    Action ();
                    if (hotTrans.ActSetsState) {
                        Log.Info (Log.LOG_STATE, LogLine (string.Format ("SM{0}: S={1} & E={2}/{3} => S={4}",
                            NameAndId (), StateName (oldState), EventName [FireEventCode], fireEvent.Mnemonic, StateName (State)), Message));
                    } else {
                        State = NextState;
                    }
                    if (null != TransIndication) {
                        TransIndication ();
                    }
                    if (oldState != State && null != StateChangeIndication) {
                        StateChangeIndication ();
                    }
                } catch (Exception ex) {
                    lock (LockObj) {
                        InProcess = false;
                    }
                    Log.Error (Log.LOG_SYS, "FireLoop Exception: {0}", ex);
                    throw;
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
                    foreach (var target in stateNode.On) {
                        if (target.ActSetsState || target.State == (uint)St.Stop) {
                            continue; // can't check
                        }
                        var targetNode = TransTable.Where (x => target.State == x.State).SingleOrDefault ();
                        if (null == targetNode) {
                            errors.Add (string.Format ("{0}: State {1}, event code {2} has invalid target state {3}",
                                PseudoKlass,
                                StateName (stateNode.State),
                                EventName [eventCode],
                                StateName (target.State)));
                        }
                    }
                    var forEventCode = stateNode.On.Where (x => eventCode == x.Event).ToList ();
                    switch (forEventCode.Count) {
                    case 1:
                        // Ensure not in Drop nor Invalid.
                        if (null != stateNode.Drop && Array.Exists (stateNode.Drop, y => eventCode == y)) {
                            errors.Add (string.Format ("{2}: State {0}, event code {1} exists both in Drop and Node.", 
                                StateName (stateNode.State),
                                EventName [eventCode],
                                PseudoKlass));
                        }
                        if (null != stateNode.Invalid && Array.Exists (stateNode.Invalid, y => eventCode == y)) {
                            errors.Add (string.Format ("{2}: State {0}, event code {1} exists both in Invalid and Node.", 
                                StateName (stateNode.State),
                                EventName [eventCode],
                                PseudoKlass));
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
                            errors.Add (string.Format ("{2}: State {0}, event code {1} exists both in Invalid and Drop.",
                                StateName (stateNode.State),
                                EventName [eventCode],
                                PseudoKlass));
                        } else if (!inDrop && !inInvalid) {
                            errors.Add (string.Format ("{2}: State {0}, event code {1} exists in none of Node, Drop nor Invalid.",
                                StateName (stateNode.State),
                                EventName [eventCode],
                                PseudoKlass));
                        }
                        break;
                    default:
                        // Event is in Node multiple times.
                        errors.Add (string.Format ("{2}: State {0}, event code {1} exists in multiple Trans in same Node.",
                            StateName (stateNode.State),
                            EventName [eventCode],
                            PseudoKlass));
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
