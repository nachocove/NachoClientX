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
        /// <summary>
        /// The number of seconds for the timeout timer for this command.  Return 0.0 to use the default timeout.
        /// </summary>
        double TimeoutInSeconds { get; }

        Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender);
        Event PreProcessResponse (AsHttpOperation Sender, NcHttpResponse response);
        Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, CancellationToken cToken);
        Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken);
        void PostProcessEvent (Event evt);
        Event ProcessTopLevelStatus (AsHttpOperation Sender, uint status, XDocument doc);
        bool SafeToXDocument (AsHttpOperation Sender, out XDocument doc);
        bool SafeToMime (AsHttpOperation Sender, out FileStream mime);
        Uri ServerUri (AsHttpOperation Sender, bool isEmailRedacted = false);
        void ServerUriChanged (Uri ServerUri, AsHttpOperation Sender);
        HttpMethod Method (AsHttpOperation Sender);
        bool UseWbxml (AsHttpOperation Sender);
        bool IgnoreBody (AsHttpOperation Sender);
        bool DoSendPolicyKey (AsHttpOperation Sender);
        // TODO: is this really a good idea?
        void StatusInd (bool didSucceed);
        void StatusInd (NcResult result);
        bool WasAbleToRephrase ();
        void ResolveAllFailed (NcResult.WhyEnum why);
        void ResolveAllDeferred ();
    }
}

