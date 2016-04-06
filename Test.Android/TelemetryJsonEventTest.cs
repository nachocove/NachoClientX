//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using NachoCore;
using NachoCore.Utils;

namespace Test.Common
{
    public class TelemetryJsonEventTest : NcTestBase
    {
        protected DateTime GetUtcNow ()
        {
            var now = DateTime.UtcNow;
            Thread.Sleep (1); // make sure we get a new millisecond.
            return now;
        }

        protected void CheckCommonHeader (DateTime now, TelemetryJsonEvent decoded)
        {
            Assert.True (!String.IsNullOrEmpty (decoded.client));
            Assert.True (now.Ticks < decoded.Timestamp ().Ticks);
            Assert.AreEqual (16, decoded.client.Length);
            Assert.AreEqual ("Ncho", decoded.client.Substring (0, 4));
            for (int n = 4; n < 16; n++) {
                var ch = decoded.client [n];
                Assert.True (Char.IsDigit (ch) || (('a' <= ch) && (ch <= 'f')));
            }
        }

        [Test]
        public void TelemetryLogEvent ()
        {
            var now = GetUtcNow ();
            var jsonEvent1 = new TelemetryLogEvent (TelemetryEventType.ERROR) {
                thread_id = 2,
                message = "This is an error",
            };
            var json1 = jsonEvent1.ToJson ();
            var decoded1 = JsonConvert.DeserializeObject<TelemetryLogEvent> (json1);
            CheckCommonHeader (now, decoded1);
            Assert.AreEqual (jsonEvent1.thread_id, decoded1.thread_id);
            Assert.AreEqual (jsonEvent1.message, decoded1.message);


            now = GetUtcNow ();
            var jsonEvent2 = new TelemetryLogEvent (TelemetryEventType.WARN) {
                thread_id = 3,
                message = "This is a warning",
            };
            var json2 = jsonEvent2.ToJson ();
            var decode2 = JsonConvert.DeserializeObject<TelemetryLogEvent> (json2);
            CheckCommonHeader (now, decode2);
            Assert.AreEqual (jsonEvent2.thread_id, decode2.thread_id);
            Assert.AreEqual (jsonEvent2.message, decode2.message);

            now = GetUtcNow ();
            var jsonEvent3 = new TelemetryLogEvent (TelemetryEventType.INFO) {
                thread_id = 4,
                message = "This is an info",
            };
            var json3 = jsonEvent3.ToJson ();
            var decode3 = JsonConvert.DeserializeObject<TelemetryLogEvent> (json3);
            CheckCommonHeader (now, decode3);
            Assert.AreEqual (jsonEvent3.thread_id, decode3.thread_id);
            Assert.AreEqual (jsonEvent3.message, decode3.message);

            now = GetUtcNow ();
            var jsonEvent4 = new TelemetryLogEvent (TelemetryEventType.DEBUG) {
                thread_id = 5,
                message = "This is a debug",
            };
            var json4 = jsonEvent4.ToJson ();
            var decode4 = JsonConvert.DeserializeObject<TelemetryLogEvent> (json4);
            Assert.AreEqual (jsonEvent4.thread_id, decode4.thread_id);
            Assert.AreEqual (jsonEvent4.message, decode4.message);
        }

        [Test]
        public void TelemetryWbxmlEvent ()
        {
            var now = GetUtcNow ();
            var jsonEvent1 = new TelemetryProtocolEvent (TelemetryEventType.WBXML_REQUEST) {
                payload = new byte[3] { 0x1, 0x2, 0x3 },
            };
            var json1 = jsonEvent1.ToJson ();
            var decode1 = JsonConvert.DeserializeObject<TelemetryProtocolEvent> (json1);
            CheckCommonHeader (now, decode1);
            Assert.AreEqual (jsonEvent1.payload, decode1.payload);

            now = GetUtcNow ();
            var jsonEvent2 = new TelemetryProtocolEvent (TelemetryEventType.WBXML_RESPONSE) {
                payload = new byte[4] { 0x9, 0x8, 0x7, 0x6 },
            };
            var json2 = jsonEvent2.ToJson ();
            var decode2 = JsonConvert.DeserializeObject<TelemetryProtocolEvent> (json2);
            CheckCommonHeader (now, decode2);
            Assert.AreEqual (jsonEvent2.payload, decode2.payload);
        }

        [Test]
        public void TelemetryUiEvent ()
        {
            var now = GetUtcNow ();
            var jsonEvent1 = new TelemetryUiEvent () {
                ui_type = "UITableView",
                ui_object = "MessageListViewController",
                ui_long = 14,
            };
            var json1 = jsonEvent1.ToJson ();
            var decode1 = JsonConvert.DeserializeObject<TelemetryUiEvent> (json1);
            CheckCommonHeader (now, decode1);
            Assert.AreEqual (jsonEvent1.ui_type, decode1.ui_type);
            Assert.AreEqual (jsonEvent1.ui_object, decode1.ui_object);
            Assert.AreEqual (jsonEvent1.ui_long, decode1.ui_long);

            now = GetUtcNow ();
            var jsonEvent2 = new TelemetryUiEvent () {
                ui_type = "UITextField",
                ui_object = "Password",
                ui_string = "abc123",
            };
            var json2 = jsonEvent2.ToJson ();
            var decode2 = JsonConvert.DeserializeObject<TelemetryUiEvent> (json2);
            CheckCommonHeader (now, decode2);
            Assert.AreEqual (jsonEvent2.ui_type, decode2.ui_type);
            Assert.AreEqual (jsonEvent2.ui_object, decode2.ui_object);
            Assert.AreEqual (jsonEvent2.ui_string, decode2.ui_string);
        }

        [Test]
        public void TelemetrySupportEvent ()
        {
            var now = GetUtcNow ();
            var jsonEvent = new TelemetrySupportEvent () {
                support = "Support request",
            };
            var json = jsonEvent.ToJson ();
            var decode = JsonConvert.DeserializeObject<TelemetrySupportEvent> (json);
            CheckCommonHeader (now, decode);
            Assert.AreEqual (jsonEvent.support, decode.support);
        }

        [Test]
        public void TelemetryStatistics2Event ()
        {
            var now = GetUtcNow ();
            var jsonEvent = new TelemetryStatistics2Event () {
                stat2_name = "Foreground time",
                count = 100,
                min = -10,
                max = +20,
                sum = 0,
                sum2 = 1000,
            };
            var json = jsonEvent.ToJson ();
            var decode = JsonConvert.DeserializeObject<TelemetryStatistics2Event> (json);
            CheckCommonHeader (now, decode);
            Assert.AreEqual (jsonEvent.stat2_name, decode.stat2_name);
            Assert.AreEqual (jsonEvent.count, decode.count);
            Assert.AreEqual (jsonEvent.min, decode.min);
            Assert.AreEqual (jsonEvent.max, decode.max);
            Assert.AreEqual (jsonEvent.sum, decode.sum);
            Assert.AreEqual (jsonEvent.sum2, decode.sum2);
        }

        [Test]
        public void TelemetryCounterEvent ()
        {
            var now = GetUtcNow ();
            var jsonEvent = new TelemetryCounterEvent () {
                counter_name = "Number_of_Clicks",
                count = 432,
                counter_start = TelemetryJsonEvent.AwsDateTime (new DateTime (2015, 5, 26, 11, 30, 00)),
                counter_end = TelemetryJsonEvent.AwsDateTime (new DateTime (2015, 5, 26, 12, 30, 00)),
            };
            var json = jsonEvent.ToJson ();
            var decode = JsonConvert.DeserializeObject<TelemetryCounterEvent> (json);
            CheckCommonHeader (now, decode);
            Assert.AreEqual (jsonEvent.counter_name, decode.counter_name);
            Assert.AreEqual (jsonEvent.count, decode.count);
            Assert.AreEqual (jsonEvent.counter_start, decode.counter_start);
            Assert.AreEqual (jsonEvent.counter_end, decode.counter_end);
        }

        [Test]
        public void TelemetrySamplesEvent ()
        {
            var now = GetUtcNow ();
            var jsonEvent = new TelemetrySamplesEvent () {
                sample_name = "Memory usage",
                sample_int = 70,
            };
            var json = jsonEvent.ToJson ();
            var decode = JsonConvert.DeserializeObject<TelemetrySamplesEvent> (json);
            CheckCommonHeader (now, decode);
            Assert.AreEqual (jsonEvent.sample_name, decode.sample_name);
            Assert.AreEqual (jsonEvent.sample_int, decode.sample_int);

            now = GetUtcNow ();
            var jsonEvent2 = new TelemetrySamplesEvent () {
                sample_name = "Object score",
                sample_float = 0.5125,
            };
            var json2 = jsonEvent2.ToJson ();
            var decode2 = JsonConvert.DeserializeObject<TelemetrySamplesEvent> (json2);
            CheckCommonHeader (now, decode2);
            Assert.AreEqual (jsonEvent2.sample_name, decode2.sample_name);
            Assert.AreEqual (jsonEvent2.sample_float, decode2.sample_float);

            now = GetUtcNow ();
            var jsonEvent3 = new TelemetrySamplesEvent () {
                sample_name = "City",
                sample_string = "San Francisco",
            };
            var json3 = jsonEvent3.ToJson ();
            var decode3 = JsonConvert.DeserializeObject<TelemetrySamplesEvent> (json3);
            CheckCommonHeader (now, decode3);
            Assert.AreEqual (jsonEvent3.sample_name, decode3.sample_name);
            Assert.AreEqual (jsonEvent3.sample_string, decode3.sample_string);
        }
    }
}

