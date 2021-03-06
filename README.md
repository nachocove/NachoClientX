About
=====

Nacho Mail is a Xamarin/C# email application capable of ActiveSync and IMAP and targeted for enterprise customers.

NachoClientX houses nearly all of the client application code, including:

- `NachoCore`, the platform-independent code handling all non-UI tasks
- `NachoClient.iOS`, the iOS client
- `NachoClient.Android`, the Android client
- `NachoClient.Mac`, a proof-of-concept Mac app demostrating NachoCore's versatility


Development Environment
=======================

Installing Visual Studio
------------------------

To setup a deveopment environment, you'll need [Visual Studio](https://www.visualstudio.com/vs/visual-studio-mac/)
Community Edition.

When installing Visual Studio, be sure to include:

- Xamarin.iOS
- Xamarin.Android
- Xamarin.Mac
- Android SDK


Configuring Android SDK
-----------------------

Next up is configuring the Android SDK.

1. Open Visual Studio
2. Go to Tools > SDK Manager
3. Select the following under Platforms (as of June 2018, if not installed, Visual Studio will prompt to install on project open)
    - Android SDK Platform 25 (aka Android 7.1 Nougat)
    - Android SDK Platform 19 (aka Android 4.4 KitKat)
    - Android SDK Platform 15 (aka Android 4.0.3 Ice Cream Sandwich)
    - Android SDK Platform 10 (aka Android 2.3 Gingerbread)
4. Select the following under Tools (most should already be checked, but verify each)
    - Android SDK Tools
    - Android SDK Platform Tools
    - Android SDK Build Tools 25.0.2*
    - Android Emulator
    - NDK
    - Extras > Android Support Repository

\* Version is hard-coded by TokenAutoComplete, would be nice to remove exact version dependency

Configuring your env
--------------------

A few build scripts need to know where the android NDK is (TODO: see if we can elimiate this requirment).

So, add something like the following line to your .bash_profile or .bashrc file:

    export NDK=/Users/yourname/Library/Developer/Xamarin/android-sdk-macosx/ndk-bundle

This is a typical location where Visual Studio installs the android ndk, but it may not be correct for all
installations.  Be sure to find the path yourself.


Getting all other repositories
------------------------------

NachoClientX depends on several other git repositories for supporting code, mostly forks of third-party
packages.

To get all the other repositories, run:

    NachoClientX $ scripts/repos.py clone


Development Builds
==================

Development builds of Nacho Mail are done entirely in Visual Studio.  Using the target selector in the top
left of the toolbar, choose either `NachoClient.iOS` or `NachoClient.Android` in the first segment, then choose 
`Debug` in the second segment, then choose your device in the final segment.  Press Run and a build will begin,
with the app launching on your device upon build completion.


Release Builds
==============

Release builds are done from the command line only using the `build.py` script, such as:

    NachoClientX $ scripts/build.py store 3.5.0 710

There are three arguments to the script:
1. kind - alpha, beta, or store; Nacho Mail will use different names, icons, and configurations depending on the kind
2. version - Typically an MAJOR.MINOR.BUGFIX version string
3. number - A number that should increment on each build


While not necessary, it is recommended to do a build from a clean set of repositories.  The only setup you need
is a clean clone of NachoClientX in an empty folder.  The build script will take care cloning all the other repos.

````
$ git clone git@github.com:nachocove/NachoClientX
$ NachoClientX/scripts/build.py ...
````

Build Output
-------------

When run with the default arguments, the `build.py` script will result in four outputs:

1. `iOS .xarchive` - The Xcode-compatible archive, which can be used to re-export the exact build by hand using different signing options
2. `iOS .ipa` - The iOS app bundle suitable for distribution
3. `Android unsigned .apk` - The unsigned Android app bundle used by clients that want to re-sign the app
4. `Android signed .apk` - The signed Android app bundle suitable for distribution


Release Branching
-----------------
Each minor release (e.g., 3.5) gets is own branch, and bugfix releases (e.g., 3.5.0 and 3.5.1) are typically built off of that
single 3.5 branch.  The `build.py` script will automatically create this branch during the first build of a new minor version.

Every repository will be branched.

If needed, you can use `repos.py` to create a branch just for a bugfix release, but this should only be necessary if
two bugfix releases are being worked on simultaneously.  `build.py` will recognize a bugfix relase branch if present
and will choose it over the minor release branch.

Build Tagging
-------------
Every build results in each repository being tagged with the build version and number.
