// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public interface IAsHttpOperationOwner
    {
        Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender);
        Event PreProcessResponse (AsHttpOperation Sender, HttpResponseMessage response);
        Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response);
        Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc);
        void PostProcessEvent (Event evt);
        Event ProcessTopLevelStatus (AsHttpOperation Sender, uint status);
        bool SafeToXDocument (AsHttpOperation Sender, out XDocument doc);
        bool SafeToMime (AsHttpOperation Sender, out StreamContent mime);
        Uri ServerUri (AsHttpOperation Sender);
        void ServerUriChanged (Uri ServerUri, AsHttpOperation Sender);
        HttpMethod Method (AsHttpOperation Sender);
        bool UseWbxml (AsHttpOperation Sender);
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

