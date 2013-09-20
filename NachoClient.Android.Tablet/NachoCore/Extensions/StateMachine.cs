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
	public enum Ev : uint {Launch, Success, Failure, Rejection, Last = Failure};
	// { state => { event => [handlers, ...]}}.
	// All possible events must be covered.
	// 1st handler in list is the event hander (required).
	public enum St : uint {Start, Stop, Last = Stop};
	public class Node {
		public uint State { set; get; }
		public Trans[] On { set; get; }
	}
	public class Trans {
		public uint State { set; get; }
		public Cb Act { set; get; }
		public uint Event { set; get; }
	}
	public class StateMachine {
		public string Name { set; get; }
		public Type LocalEventType { set; get; }
		public Type LocalStateType { set; get; }
		public Node[] TransTable { set; get; }
		public uint State { set; get; }
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
			ProcEvent ((uint)Ev.Launch);
		}
		public void ProcEvent (uint Event) {
			ProcEvent (Event, null);
		}
		public void ProcEvent(uint Event, object arg) {
			if ((uint)St.Stop == State) {
				return;
			}
			EventQ.Enqueue (Tuple.Create(Event, arg));
			if (IsFiring) {
				return;
			}
			IsFiring = true;
			while (0 != EventQ.Count) {
				var tuple = (Tuple<uint,object>)EventQ.Dequeue ();
				var fireEvent = tuple.Item1;
				Arg = tuple.Item2;
				var hotNode = TransTable.Where (x => State == x.State).First ();
				var hotTrans = hotNode.On.Where (x => fireEvent == x.Event).First ();
				Console.WriteLine ("SM({0}): S={1} & E={2} => S={3}", Name, StateName (State), 
				                   EventName (fireEvent), StateName (hotTrans.State));
				Action = hotTrans.Act;
				Action ();
				State = hotTrans.State;
				if (null != StateChangeIndication) {
					StateChangeIndication ();
				}
			}
			IsFiring = false;
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
