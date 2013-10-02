# NachoClient (X is for Xamarin)

## Status/Caveats

* See the class NachoCore/NachoDemo.cs for usage of the API between the Back End and the UI.
* The BE code currently progresses through the EAS start-up sequence, and downloads all folders, as well as all the email in the default email folder.
* This has only been tested against GMail's ActiveSync.
* The BE does watch for DB updates by the UI, but currently the BE doesn't push those updates to the server. For example, if the UI deleted an email, that delete will not yet propagate to the server.
* The BE does support sending an email.

## Source Code Management

* BE changes will be developed on a branch and collapsed into master periodically.
* Stable BE handoff points on master will be tagged pre<n> such as "pre0", "pre1", ...
* BE bugfixes needed for UI development can be committed directly to master.
* UI code must go in this repo too. The UI code must support "headless"/UI-less debugging of the BE (see NachoDemo for an example).
* UI code can be developed on a branch that syncs with master, or in a forked repo. UI developer choice...

## TODO

1. Support Common Status Codes (ASCMD 2.2.4).
2. Tighent-up each command: options, provision, settings, foldersync, sync, ping, sendmail.
2. Support EAS and on-device contacts.
3. Finish implementing Autodiscovery.
4. Support attachments.
5. Add support for XML Schema validation of messages.
