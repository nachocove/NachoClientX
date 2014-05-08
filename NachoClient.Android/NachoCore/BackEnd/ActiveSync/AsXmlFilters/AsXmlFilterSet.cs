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

        public  static NcXmlFilterSet Requests {
            get {
                if (null == _Requests) {
                    _Requests = new NcXmlFilterSet ();
                }
                return _Requests;
            }
        }

        public static NcXmlFilterSet Responses {
            get {
                if (null == _Responses) {
                    _Responses = new NcXmlFilterSet ();
                }
                return _Responses;
            }
        }

        public static void Initialize ()
        {
            AsXmlFilterSet.Requests.Add (new AsXmlFilterAirSyncRequest ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterComposeMailRequest ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterFolderHierarchyRequest ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterProvisionRequest ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterSettingsRequest ());

            AsXmlFilterSet.Requests.Add (new AsXmlFilterAirSyncBase ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterCalendar ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterContacts ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterContacts2 ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterDocumentLibrary ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterEmail ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterEmail2 ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterItemOperations ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterPingRequest ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterRightsManagement ());
            AsXmlFilterSet.Requests.Add (new AsXmlFilterTasks ());

            AsXmlFilterSet.Responses.Add (new AsXmlFilterAirSyncResponse ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterComposeMailResponse ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterFolderHierarchyResponse ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterProvisionResponse ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterSettingsResponse ());

            AsXmlFilterSet.Responses.Add (new AsXmlFilterAirSyncBase ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterCalendar ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterContacts ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterContacts2 ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterDocumentLibrary ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterEmail ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterEmail2 ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterItemOperations ());          
            AsXmlFilterSet.Responses.Add (new AsXmlFilterPingResponse ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterRightsManagement ());
            AsXmlFilterSet.Responses.Add (new AsXmlFilterTasks ());
        }
    }
}

