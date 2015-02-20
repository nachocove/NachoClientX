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
}

