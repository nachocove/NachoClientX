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
		public Node[] TransTable { set; get; }
		public uint State { set; get; }
		public Cb Action { set; get; }
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
		public void ProcEvent(uint Event) {
			if ((uint)St.Stop == State) {
				return;
			}
			EventQ.Enqueue (Event);
			if (IsFiring) {
				return;
			}
			IsFiring = true;
			while (0 != EventQ.Count) {
				var fireEvent = (uint)EventQ.Dequeue ();
				var hotNode = TransTable.Where (x => State == x.State).First ();
				var hotTrans = hotNode.On.Where (x => fireEvent == x.Event).First ();
				hotTrans.Act ();
				Console.WriteLine ("SM: S={0} & E={1} => S={2}", State, fireEvent, hotTrans.State);
				State = hotTrans.State;
			}
			IsFiring = false;
		}
	}
}
