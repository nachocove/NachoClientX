using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    // If we can communicate with the server, and we can't tell that the problem is transient,
    // then we have a HardFail.
	// Precise events are used to indicate value-based failures (e.g. credential, server config, etc).
	public enum Ev : uint {Launch, Success, HardFail, TempFail, Last = TempFail};
	// { state => { event => [handlers, ...]}}.
	// All possible events must be covered.
	// 1st handler in list is the event hander (required).
	public enum St : uint {Start, Stop, Last = Stop};
	public class Node {
		public uint State { set; get; }
		public Trans[] On { set; get; }
		public uint[] Drop { set; get; }
		public uint[] Invalid { set; get; }
	}
	public class Trans {
		public uint State { set; get; }
		public Cb Act { set; get; }
		public uint Event { set; get; }
	}
    public class Event {
        public uint EventCode { get; set; }
        public object Arg { get; set; }

        public static Event Create (uint eventCode) {
            return new Event () { EventCode = eventCode };
        }

        public static Event Create (uint eventCode, object arg) {
            return new Event () { EventCode = eventCode, Arg = arg };
        }
    }

	public sealed class StateMachine {
		public string Name { set; get; }
		public Type LocalEventType { set; get; }
		public Type LocalStateType { set; get; }
		public Node[] TransTable { set; get; }
		public uint State { set; get; }
        public uint NextState { set; get; }
        public uint FireEvent { set; get; }
		public Cb Action { set; get; }
		public object Arg { set; get; }
		public Cb StateChangeIndication { set; get; }
		private Queue EventQ { set; get; }
		private bool IsFiring { set; get; }
		public StateMachine() {
			EventQ = new Queue ();
			State = (uint)St.Start;
		}
		public void Start() {
			Start((uint)St.Start);
		}
		public void Start(uint StartState) {
			State = StartState;
			PostEvent ((uint)Ev.Launch);
		}

		public void PostAtMostOneEvent (uint Event) {
			if (! EventQ.Contains (Event)) {
				PostEvent (Event);
			}
		}

		public void PostEvent (uint eventCode) {
			PostEvent (eventCode, null);
		}
		
        public void PostEvent (Event smEvent) {
            PostEvent (smEvent.EventCode, smEvent.Arg);
        }

		public void PostEvent (uint eventCode, object arg) {
			if ((uint)St.Stop == State) {
				return;
			}
			EventQ.Enqueue (Tuple.Create(eventCode, arg));
			if (IsFiring) {
				return;
			}
			IsFiring = true;
			while (0 != EventQ.Count) {
				var tuple = (Tuple<uint,object>)EventQ.Dequeue ();
				FireEvent = tuple.Item1;
				Arg = tuple.Item2;
				var hotNode = TransTable.Where (x => State == x.State).First ();
				if (null != hotNode.Drop && hotNode.Drop.Contains (FireEvent)) {
					Console.WriteLine ("SM({0}): S={1} & E={2} => DROPPED EVENT", Name, StateName (State), EventName (FireEvent));
					continue;
				}
				if (null != hotNode.Invalid && hotNode.Invalid.Contains (FireEvent)) {
					Console.WriteLine ("SM({0}): S={1} & E={2} => INVALID EVENT", Name, StateName (State), EventName (FireEvent));
					throw new Exception ();
				}
				var hotTrans = hotNode.On.Where (x => FireEvent == x.Event).Single ();
				Console.WriteLine ("SM({0}): S={1} & E={2} => S={3}", Name, StateName (State), 
				                   EventName (FireEvent), StateName (hotTrans.State));
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
            var errors = new List<string> ();
            var eventCodes = Enum.GetValues (typeof(Ev));
            if (null != LocalEventType) {
                var localCodes = Enum.GetValues (LocalEventType);
                var allCodes = new uint[eventCodes.Length + localCodes.Length];
                eventCodes.CopyTo (allCodes, 0);
                localCodes.CopyTo (allCodes, eventCodes.Length);
                eventCodes = allCodes;
            }
            foreach (var stateNode in TransTable) {
                foreach (var enumEventCode in eventCodes) {
                    var eventCode = (uint)enumEventCode;
                    var forEventCode = stateNode.On.Where (x => eventCode == x.Event).ToList ();
                    switch (forEventCode.Count) {
                    case 1:
                        // Ensure not in Drop nor Invalid.
                        if (null != stateNode.Drop && Array.Exists (stateNode.Drop, y => eventCode == y)) {
                            errors.Add (string.Format ("State {0}, event code {1} exists both in Drop and Node.", 
                                                       StateName (stateNode.State),
                                                       EventName (eventCode)));
                        }
                        if (null != stateNode.Invalid && Array.Exists (stateNode.Invalid, y => eventCode == y)) {
                            errors.Add (string.Format ("State {0}, event code {1} exists both in Invalid and Node.", 
                                                       StateName (stateNode.State),
                                                       EventName (eventCode)));
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
                                                       EventName (eventCode)));
                        } else if (!inDrop && !inInvalid) {
                            errors.Add (string.Format ("State {0}, event code {1} exists in none of Node, Drop nor Invalid.",
                                                       StateName (stateNode.State),
                                                       EventName (eventCode)));
                        }
                        break;
                    default:
                        // Event is in Node multiple times.
                        errors.Add (string.Format ("State {0}, event code {1} exists in multiple Trans in same Node.",
                                                   StateName (stateNode.State),
                                                   EventName (eventCode)));
                        break;
                    }
                }
            }
            foreach (var error in errors) {
                Console.WriteLine (error);
            }
            if (0 != errors.Count) {
                throw new Exception (string.Format ("State machine {0} needs to be rectified.", Name));
            }
        }

		private string StateName (uint state) {
			if ((uint)St.Last < (uint)state) {
				if (null != LocalStateType) {
					return Enum.GetName (LocalStateType, state);
				}
				return state.ToString ();
			}
			return Enum.GetName (typeof(St), state);
		}
		private string EventName (uint evt) {
			if ((uint)Ev.Last < (uint)evt) {
				if (null != LocalEventType) {
					return Enum.GetName (LocalEventType, evt);
				}
				return evt.ToString ();
			}
			return Enum.GetName (typeof(Ev), evt);
		}
	}
}
