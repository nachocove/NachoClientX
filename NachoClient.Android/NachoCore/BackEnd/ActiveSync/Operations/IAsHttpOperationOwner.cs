// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public interface IAsHttpOperationOwner
    {
        Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender);
        Event PreProcessResponse (AsHttpOperation Sender, HttpResponseMessage response);
        Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, CancellationToken cToken);
        Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc, CancellationToken cToken);
        void PostProcessEvent (Event evt);
        Event ProcessTopLevelStatus (AsHttpOperation Sender, uint status, XDocument doc);
        bool SafeToXDocument (AsHttpOperation Sender, out XDocument doc);
        bool SafeToMime (AsHttpOperation Sender, out Stream mime);
        Uri ServerUri (AsHttpOperation Sender, bool isEmailRedacted = false);
        void ServerUriChanged (Uri ServerUri, AsHttpOperation Sender);
        HttpMethod Method (AsHttpOperation Sender);
        bool UseWbxml (AsHttpOperation Sender);
        bool IgnoreBody (AsHttpOperation Sender);
        bool IsContentLarge (AsHttpOperation Sender);
        bool DoSendPolicyKey (AsHttpOperation Sender);
        // TODO: is this really a good idea?
        void StatusInd (bool didSucceed);
        void StatusInd (NcResult result);
        bool WasAbleToRephrase ();
        void ResolveAllFailed (NcResult.WhyEnum why);
        void ResolveAllDeferred ();
    }
}

