//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public static class AsXmlFilterSet
    {
        private static NcXmlFilterSet _Requests = null;
        private static NcXmlFilterSet _Responses = null;
        private static object LockObj = new object ();

        public static NcXmlFilterSet Requests {
            get {
                lock (LockObj) {
                    if (null == _Requests) {
                        _Requests = new NcXmlFilterSet ();
                        InitializeRequestFilters ();
                    }
                    return _Requests;
                }
            }
        }

        public static NcXmlFilterSet Responses {
            get {
                lock (LockObj) {
                    if (null == _Responses) {
                        _Responses = new NcXmlFilterSet ();
                        InitializeResponseFilters ();
                    }
                    return _Responses;
                }
            }
        }

        public static void InitializeRequestFilters ()
        {
            AsXmlFilterSet._Requests.Add (new AsXmlFilterAirSyncRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterComposeMailRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterFolderHierarchyRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterGetItemEstimateRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterItemOperationsRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterMeetingResponseRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterMoveRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterPingRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterProvisionRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterResolveRecipientsRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterSearchRequest ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterSettingsRequest ());

            AsXmlFilterSet._Requests.Add (new AsXmlFilterAirSyncBase ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterCalendar ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterContacts ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterContacts2 ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterDocumentLibrary ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterEmail ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterEmail2 ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterGAL ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterRightsManagement ());
            AsXmlFilterSet._Requests.Add (new AsXmlFilterTasks ());
        }

        public static void InitializeResponseFilters ()
        {
            AsXmlFilterSet._Responses.Add (new AsXmlFilterAirSyncResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterComposeMailResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterFolderHierarchyResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterGetItemEstimateResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterItemOperationsResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterMeetingResponseResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterMoveResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterPingResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterProvisionResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterResolveRecipientsResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterSearchResponse ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterSettingsResponse ());

            AsXmlFilterSet._Responses.Add (new AsXmlFilterAirSyncBase ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterCalendar ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterContacts ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterContacts2 ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterDocumentLibrary ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterEmail ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterEmail2 ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterGAL ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterRightsManagement ());
            AsXmlFilterSet._Responses.Add (new AsXmlFilterTasks ());
        }
    }

    public class AutoDiscoverXmlFilter : NcXmlFilter
    {
        public AutoDiscoverXmlFilter () : base (new[]{"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006", "http://schemas.microsoft.com/exchange/autodiscover/responseschema/2006"})
        {
            Root = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            var autoDRoot = new NcXmlFilterNode ("Autodiscover", RedactionType.NONE, RedactionType.NONE);

            var responseNode = new NcXmlFilterNode ("Response", RedactionType.NONE, RedactionType.NONE);
            {
                {
                    responseNode.Add (new NcXmlFilterNode ("Culture", RedactionType.NONE, RedactionType.NONE));
                }
                {
                    var userNode = new NcXmlFilterNode ("User", RedactionType.NONE, RedactionType.NONE);
                    {
                        var emailNode = new NcXmlFilterNode ("EMailAddress", RedactionType.FULL, RedactionType.FULL);
                        var displayNameNode = new NcXmlFilterNode ("DisplayName", RedactionType.FULL, RedactionType.FULL);
                        userNode.Add (emailNode);
                        userNode.Add (displayNameNode);
                    }
                    responseNode.Add (userNode);
                }
                {
                    var actionNode = new NcXmlFilterNode ("Action", RedactionType.NONE, RedactionType.NONE);
                    {
                        var redirectNode = new NcXmlFilterNode ("Redirect", RedactionType.FULL, RedactionType.FULL);
                        actionNode.Add (redirectNode);
                
                        var settingsNode = new NcXmlFilterNode ("Settings", RedactionType.NONE, RedactionType.NONE);
                        {
                            var serverNode = new NcXmlFilterNode ("Server", RedactionType.NONE, RedactionType.NONE);
                            serverNode.Add (new NcXmlFilterNode ("Type", RedactionType.NONE, RedactionType.NONE));
                            serverNode.Add (new NcXmlFilterNode ("Url", RedactionType.NONE, RedactionType.NONE));
                            serverNode.Add (new NcXmlFilterNode ("Name", RedactionType.NONE, RedactionType.NONE));
                            serverNode.Add (new NcXmlFilterNode ("ServerData", RedactionType.NONE, RedactionType.NONE));
                            settingsNode.Add (serverNode);
                        }
                        actionNode.Add (settingsNode);
                    }
                    var errorNode = new NcXmlFilterNode ("Error", RedactionType.NONE, RedactionType.NONE);
                    {
                        errorNode.Add (new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE));
                        errorNode.Add (new NcXmlFilterNode ("Message", RedactionType.NONE, RedactionType.NONE));
                        errorNode.Add (new NcXmlFilterNode ("ErrorCode", RedactionType.NONE, RedactionType.NONE));
                        errorNode.Add (new NcXmlFilterNode ("DebugData", RedactionType.NONE, RedactionType.NONE));
                        actionNode.Add (errorNode);
                    }
                    responseNode.Add (actionNode);
                }
            }
            autoDRoot.Add (responseNode);
            Root.Add (autoDRoot);
        }
    }


}

