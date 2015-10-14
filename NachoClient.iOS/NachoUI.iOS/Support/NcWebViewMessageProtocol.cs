//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using System.Collections.Generic;
using NachoPlatform;

namespace NachoClient.iOS
{
    // WKWebView has a way for Javascript running in a web page to call back to objective-c/c# code.
    // Communication from JS makes certain things easier, like knowing when the text in the web view has changed.
    // What follows is an approximation of what WKWebView does, making use of a custom protocol on this end and 
    // an XMLHttpRequest to a URL of that protocol on the JS end.
    // The custom protocol deciphers the message from the URL query string and dispatches the info to a delegate
    // similar to WKScriptMessageHandler

    public class NcWebViewMessage
    {
        public readonly string Name;
        public readonly NSObject Body;

        public NcWebViewMessage (string name, NSObject body)
        {
            Name = name;
            Body = body;
        }
    }

    public interface NcWebViewMessageHandler {
        void HandleWebViewMessage (NcWebViewMessage message);
    }

    public class NcWebViewMessageProtocol : NSUrlProtocol 
    {

        static bool Registered = false;
        static Dictionary<string, List<NcWebViewMessageHandler>> Handlers;

        static void Register ()
        {
            if (!Registered) {
                NSUrlProtocol.RegisterClass (new ObjCRuntime.Class (typeof(NcWebViewMessageProtocol)));
                Registered = true;
            }
        }

        static void Unregister ()
        {
            if (Registered) {
                NSUrlProtocol.UnregisterClass (new ObjCRuntime.Class (typeof(NcWebViewMessageProtocol)));
                Registered = false;
            }
        }

        public static void AddHandler (NcWebViewMessageHandler handler, string name)
        {
            if (Handlers == null) {
                Handlers = new Dictionary<string, List<NcWebViewMessageHandler>> ();
            }
            Register ();
            if (!Handlers.ContainsKey (name)) {
                Handlers [name] = new List<NcWebViewMessageHandler> ();
            }
            Handlers [name].Add (handler);
        }

        public static void RemoveHandler (NcWebViewMessageHandler handler, string name)
        {
            if (Handlers != null) {
                if (Handlers.ContainsKey (name)) {
                    Handlers [name].Remove (handler);
                    if (Handlers [name].Count == 0) {
                        Handlers.Remove (name);
                        if (Handlers.Count == 0) {
                            Unregister ();
                        }
                    }
                }
            }
        }

        [Export ("canInitWithRequest:")]
        public static bool canInitWithRequest (NSUrlRequest request)
        {
            if ((null == request) || (null == request.Url)) {
                return false;
            }
            return request.Url.Scheme == "nachomessage";
        }

        [Export ("canonicalRequestForRequest:")]
        public static new NSUrlRequest GetCanonicalRequest (NSUrlRequest forRequest)
        {
            return forRequest;
        }

        [Export ("initWithRequest:cachedResponse:client:")]
        public NcWebViewMessageProtocol (NSUrlRequest request, NSCachedUrlResponse cachedResponse, INSUrlProtocolClient client) : base (request, cachedResponse, client)
        {
        }

        public override void StartLoading ()
        {
            if ((null == Request) || (null == Request.Url)) {
                return;
            }
            var name = Request.Url.Host;
            var body = new NSMutableDictionary ();
            var components = new NSUrlComponents (Request.Url, false);
            foreach (var item in components.QueryItems) {
                var key = new NSString (item.Name);
                var value = new NSString (item.Value);
                body.SetValueForKey (value, key);
            }
            var message = new NcWebViewMessage (name, body);
            using (var response = new NSUrlResponse (Request.Url, "text/plain", 0, "utf8")) {
                Client.ReceivedResponse (this, response, NSUrlCacheStoragePolicy.NotAllowed);
            }
            Client.FinishedLoading (this);
            Dispatch (message);
        }

        public override void StopLoading ()
        {
        }

        void Dispatch (NcWebViewMessage message)
        {
            if (Handlers.ContainsKey (message.Name)) {
                foreach (var handler in Handlers[message.Name]) {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        handler.HandleWebViewMessage (message);
                    });
                }
            }
        }

    }
}

