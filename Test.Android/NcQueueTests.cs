//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NUnit.Framework;

namespace Test.Common
{
    public class NcQueueTests : NcTestBase
    {
        class NcQueueTestItem : NcQueueElement
        {
            public int Id { get; set; }

            public NcQueueTestItem (int id)
            {
                Id = id;
            }

            public uint GetSize ()
            {
                return sizeof(int);
            }
        }

        [Test]
        public void NcQueueAddItems ()
        {
            var queue = new NcQueue<NcQueueTestItem> ();
            Assert.IsTrue (queue.IsEmpty ());

            var itemOne = new NcQueueTestItem (1);
            queue.Enqueue (itemOne);

            Assert.False (queue.IsEmpty ());

            var myObj = queue.Peek ();
            Assert.AreEqual (itemOne.Id, myObj.Id);

            myObj = queue.Dequeue ();
            Assert.AreEqual (itemOne.Id, myObj.Id);

            Assert.IsTrue (queue.IsEmpty ());

            var itemTwo = new NcQueueTestItem (2);

            queue.Enqueue (itemOne);
            queue.Enqueue (itemTwo);

            Assert.False (queue.IsEmpty ());

            myObj = queue.Dequeue ();
            Assert.AreEqual (itemOne.Id, myObj.Id);

            myObj = queue.Dequeue ();
            Assert.AreEqual (itemTwo.Id, myObj.Id);

            Assert.IsTrue (queue.IsEmpty ());

            queue.Enqueue (itemOne);
            queue.Undequeue (itemTwo);

            myObj = queue.Dequeue ();
            Assert.AreEqual (itemTwo.Id, myObj.Id);

            myObj = queue.Dequeue ();
            Assert.AreEqual (itemOne.Id, myObj.Id);
        }

        [Test]
        public void NcQueueEnqueueIfNot ()
        {
            var queue = new NcQueue<NcQueueTestItem> ();
            Assert.IsTrue (queue.IsEmpty ());

            var itemOne = new NcQueueTestItem (1);
            queue.Enqueue (itemOne);

            var itemOnePrime = new NcQueueTestItem (1);
            queue.EnqueueIfNot (itemOnePrime, (obj) => {
                NcQueueTestItem item = obj;
                return item.Id == itemOnePrime.Id;
            });

            Assert.False (queue.IsEmpty ());

            var myObj = queue.Dequeue ();
            Assert.AreEqual (itemOne.Id, myObj.Id);

            Assert.IsTrue (queue.IsEmpty ());

            queue.Enqueue (itemOne);

            var itemTwo = new NcQueueTestItem (2);
            queue.EnqueueIfNot (itemTwo, (obj) => {
                NcQueueTestItem item = obj;
                return item.Id == itemTwo.Id;
            });

            myObj = queue.Dequeue ();
            Assert.AreEqual (itemOne.Id, myObj.Id);

            myObj = queue.Dequeue ();
            Assert.AreEqual (itemTwo.Id, myObj.Id);

            Assert.IsTrue (queue.IsEmpty ());
        }

        [Test]
        public void NcQueueUndequeueIfNot ()
        {
            var queue = new NcQueue<NcQueueTestItem> ();
            Assert.IsTrue (queue.IsEmpty ());

            var itemOne = new NcQueueTestItem (1);
            queue.Enqueue (itemOne);

            var itemOnePrime = new NcQueueTestItem (1);
            queue.UndequeueIfNot (itemOnePrime, (obj) => {
                NcQueueTestItem item = obj;
                return item.Id == itemOnePrime.Id;
            });

            Assert.False (queue.IsEmpty ());

            var myObj = queue.Dequeue ();
            Assert.AreEqual (itemOne.Id, myObj.Id);

            Assert.IsTrue (queue.IsEmpty ());

            queue.Enqueue (itemOne);

            var itemTwo = new NcQueueTestItem (2);
            queue.UndequeueIfNot (itemTwo, (obj) => {
                NcQueueTestItem item = obj;
                return item.Id == itemTwo.Id;
            });

            myObj = queue.Dequeue ();
            Assert.AreEqual (itemTwo.Id, myObj.Id);

            myObj = queue.Dequeue ();
            Assert.AreEqual (itemOne.Id, myObj.Id);

            Assert.IsTrue (queue.IsEmpty ());
        }
    }
}

