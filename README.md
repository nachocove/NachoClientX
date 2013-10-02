# NachoClient (X is for Xamarin)

## Status/Caveats

* See the class NachoCore/NachoDemo.cs for usage of the API between the Back End and the UI.
* The BE code currently progresses through the EAS start-up sequence, and downloads all folders, as well as all the email in the default email folder. After this, the BE continues to poll the server for incoming email. The UI finds out about new mail by watching for incoming DB writes by the BE.
* This has only been tested against GMail's ActiveSync.
* The BE will propagate email deltes to the server.
* The BE does support sending an email.

## Source Code Management

* BE changes will be developed on a branch and collapsed into master periodically.
* Stable BE handoff points on master will be tagged pre<n> such as "pre0", "pre1", ...
* BE bugfixes needed for UI development can be committed directly to master.
* UI code must go in this repo too. The UI code must support "headless"/UI-less debugging of the BE (see NachoDemo for an example).
* UI code can be developed on a branch that syncs with master, or in a forked repo. UI developer choice...

## TODO (in priority order)

1. Download EAS contacts.
2. Support Common Status Codes (ASCMD 2.2.4).
3. Tighent-up each command: options, provision, settings, foldersync, sync, ping, sendmail.
4. Support attachments (download & upload).
5. Finish implementing Autodiscovery.
6. support for XML Schema validation of messages.
