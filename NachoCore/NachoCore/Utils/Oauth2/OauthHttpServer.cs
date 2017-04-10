//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore
{
    public class OauthHttpServer
    {

        public int Port = 0;
        public event EventHandler<Client> OnRequest;

        TcpListener TcpServer = null;

        ConcurrentDictionary<int, Client> ClientsById;
        int ClientId = 0;
        int MaxClientCount = 4;

        bool StopAfterAllClientsClose = false;
        Action GracefulStopCompletion = null;

        object _IsRunningLock = new object();
        bool _IsRunning;
        bool IsRunning {
            get {
                lock (_IsRunningLock) {
                    return _IsRunning;
                }
            }
            set {
                lock (_IsRunningLock) {
                    _IsRunning = value;
                }
            }
        }

        public OauthHttpServer ()
        {
            ClientsById = new ConcurrentDictionary<int, Client> ();
        }

        public void Start (Action startedCallback)
        {
            if (IsRunning) {
                return;
            }
            IsRunning = true;
            NcTask.Run (() => {
                Log.Info (Log.LOG_OAUTH, "OauthHttpServer starting...");
                try {
                    var address = IPAddress.Parse ("127.0.0.1");
                    TcpServer = new TcpListener (address, Port);
                    TcpServer.Start ();
                    Port = (TcpServer.LocalEndpoint as IPEndPoint).Port;
                    InvokeOnUIThread.Instance.Invoke (() => {
                        startedCallback ();
                    });
                    IsRunning = true;
                    Log.Info (Log.LOG_OAUTH, "OauthHttpServer accepting clients");
                    while (IsRunning) {
                        var tcpClient = TcpServer.AcceptTcpClient ();
                        if (ClientsById.Count > MaxClientCount) {
                            Log.Info (Log.LOG_OAUTH, "OauthHttpServer rejecting client (more than {0} connected)", MaxClientCount);
                            tcpClient.Close ();
                        } else {
                            var client = new Client (this, ClientId++, tcpClient);
                            Log.Info (Log.LOG_OAUTH, "OauthHttpServer got client #{0}", client.Id);
                            ClientsById.TryAdd (client.Id, client);
                            client.Open ();
                        }
                    }
                } catch (Exception e) {
                    if (IsRunning) {
                        Log.Error (Log.LOG_OAUTH, "OauthHttpServer socket exception: {0}", e);
                    }
                } finally {
                    Stop ();
                }
                Log.Info (Log.LOG_OAUTH, "OauthHttpServer stopped");
            }, "OAuthHTTPServer");
        }

        public void Stop ()
        {
            if (IsRunning) {
                Log.Info (Log.LOG_OAUTH, "OauthHttpServer stopping...");
                foreach (var client in ClientsById.Values) {
                    client.Close ();
                }
                if (TcpServer != null) {
                    TcpServer.Stop ();
                }
                IsRunning = false;
            }
        }

        public void GracefulStop (Action completion)
        {
            Log.Info (Log.LOG_OAUTH, "OauthHttpServer graceful stop requested");
            if (ClientsById.Count == 0) {
                Stop ();
                if (completion != null) {
                    InvokeOnUIThread.Instance.Invoke (completion);
                }
            } else {
                StopAfterAllClientsClose = true;
                GracefulStopCompletion = completion;
            }
        }

        private void ClientDidClose (Client client)
        {
            Log.Info (Log.LOG_OAUTH, "OauthHttpServer did close client #{0}", client.Id);
            Client ignore;
            ClientsById.TryRemove (client.Id, out ignore);
            if (StopAfterAllClientsClose && ClientsById.Count == 0) {
                Stop ();
                if (GracefulStopCompletion != null) {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        GracefulStopCompletion ();
                        GracefulStopCompletion = null;
                    });
                }
            }
        }

        private void ClientDidReceiveRequest (Client client)
        {
            if (client.RequestUriString.StartsWith ("/")) {
                if (!Uri.TryCreate (String.Format ("http://127.0.0.1:{0}{1}", Port, client.RequestUriString), UriKind.Absolute, out client.RequestUri)) {
                    Log.Error (Log.LOG_OAUTH, "OauthHttpServer got bad client url: {0}", client.RequestUriString);
                }
            } else {
                if (!Uri.TryCreate (client.RequestUriString, UriKind.Absolute, out client.RequestUri)) {
                    Log.Error (Log.LOG_OAUTH, "OauthHttpServer got bad client url: {0}", client.RequestUriString);
                }
            }
            if (client.RequestUri != null) {
                OnRequest (this, client);
            }
        }

        public class Client
        {

            public int Id;
            public string RequestUriString;
            public Uri RequestUri;
            public bool _IsOpen = false;
            public object _IsOpenLock = new object ();
            public bool IsOpen {
                get {
                    lock (_IsOpenLock) {
                        return _IsOpen;
                    }
                }
                set {
                    lock (_IsOpenLock) {
                        _IsOpen = value;
                    }
                }
            }

            WeakReference<OauthHttpServer> WeakServer;
            TcpClient TcpClient;
            NetworkStream Stream;
            byte [] ReadBuffer;
            byte [] LineBuffer;
            int LineBufferOffset = 0;

            enum RequestParseState
            {
                Request,
                Headers,
                Body
            }

            RequestParseState ParseState = RequestParseState.Request;

            public Client (OauthHttpServer server, int id, TcpClient tcpClient)
            {
                Id = id;
                WeakServer = new WeakReference<OauthHttpServer> (server);
                TcpClient = tcpClient;
                Stream = TcpClient.GetStream ();
                ReadBuffer = new byte [1024];
                LineBuffer = new byte [1024];
            }

            public void Open ()
            {
                if (IsOpen) {
                    return;
                }
                IsOpen = true;
                NcTask.Run (() => {
                    Log.Info (Log.LOG_OAUTH, "OauthHttpClient task #{0} started", Id);
                    while (IsOpen) {
                        Read ();
                    }
                    Log.Info (Log.LOG_OAUTH, "OauthHttpClient task #{0} stopping...", Id);
                    OauthHttpServer server;
                    if (WeakServer.TryGetTarget (out server)) {
                        server.ClientDidClose (this);
                    }
                    Log.Info (Log.LOG_OAUTH, "OauthHttpClient task #{0} stopped", Id);
                }, "OAuthHTTPClient");
            }

            public void Close ()
            {
                try {
                    TcpClient.Close ();
                } catch (Exception e) {
                    Log.Error (Log.LOG_OAUTH, "OauthHttpClient #{0} tcp close failed: {1}", Id, e);
                }
                IsOpen = false;
            }

            public void Read ()
            {
                try {
                    int lengthRead = Stream.Read (ReadBuffer, 0, ReadBuffer.Length);
                    if (lengthRead <= 0) {
                        Close ();
                    } else {
                        Parse (lengthRead);
                    }
                } catch (Exception e) {
                    Log.Error (Log.LOG_OAUTH, "OauthHttpClient read exception: {0}", e);
                } finally {
                    Close ();
                }
            }

            void Parse (int length)
            {
                Log.Info (Log.LOG_OAUTH, "OauthHttpClient #{0} received {1} bytes", Id, length);
                if (ParseState != RequestParseState.Body) {
                    int i = 0;
                    while (i < length) {
                        if (LineBufferOffset < LineBuffer.Length) {
                            LineBuffer [LineBufferOffset] = ReadBuffer [i];
                            LineBufferOffset += 1;
                            if (LineBufferOffset > 0 && LineBuffer [LineBufferOffset - 1] == 0x0A) {
                                if (LineBufferOffset > 1 && LineBuffer [LineBufferOffset - 2] == 0x0D) {
                                    LineBufferOffset -= 2;
                                } else {
                                    LineBufferOffset -= 1;
                                }
                                ReceiveLine ();
                                LineBufferOffset = 0;
                            }
                            i += 1;
                        } else {
                            // Just kill the connection if there's a long status or header line, we don't expect one
                            Log.Warn (Log.LOG_OAUTH, "OauthHttpClient #{0} got too long of a line", Id);
                            Close ();
                            i = length;
                        }
                    }
                } else {
                    // Just kill the connection if there's a body in the request, we don't expect one
                    Log.Warn (Log.LOG_OAUTH, "OauthHttpClient #{0} got body data", Id);
                    Close ();
                }
            }

            void ReceiveLine ()
            {
                switch (ParseState) {
                case RequestParseState.Request:
                    var requestLine = System.Text.Encoding.UTF8.GetString (LineBuffer, 0, LineBufferOffset).Trim ();
                    var parts = requestLine.Split (new char [] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 3 || parts [0] != "GET" || parts [2] != "HTTP/1.1") {
                        // Just kill the connection if there's an unexpected request
                        Log.Warn (Log.LOG_OAUTH, "OauthHttpClient #{0} got unexpected request line: {1}", Id, requestLine);
                        Close ();
                    } else {
                        RequestUriString = parts [1];
                        ParseState = RequestParseState.Headers;
                        Log.Info (Log.LOG_OAUTH, "OauthHttpClient #{0} parsed request line {1}", Id, requestLine);
                    }
                    break;
                case RequestParseState.Headers:
                    if (LineBufferOffset == 0) {
                        Log.Info (Log.LOG_OAUTH, "OauthHttpClient #{0} parsed headers", Id);
                        ParseState = RequestParseState.Body;
                        OauthHttpServer server;
                        if (WeakServer.TryGetTarget (out server)) {
                            server.ClientDidReceiveRequest (this);
                        }
                    }
                    break;
                }
            }

            public void Write (byte [] data)
            {
                Stream.Write (data, 0, data.Length);
            }

            public void Write (string str)
            {
                var encoded = System.Text.Encoding.UTF8.GetBytes (str);
                Write (encoded);
            }

            public void Send (int statusCode, string statusText, string responseHtml)
            {
                var bodyData = System.Text.Encoding.UTF8.GetBytes (responseHtml);
                var statusLine = String.Format ("HTTP/1.1 {0} {1}\r\n", statusCode, statusText);
                Write (statusLine);
                var headers = new string [] {
                    "Content-Type: text/html; charset=utf8",
                    String.Format("Content-Length: {0}", bodyData.Length),
                    "Connection: close"
                };
                foreach (var header in headers) {
                    Write (header);
                    Write ("\r\n");
                }
                Write ("\r\n");
                Write (bodyData);
                Log.Info (Log.LOG_OAUTH, "OauthHttpClient #{0} sent response {1}", Id, statusCode);
            }
        }
    }
}
