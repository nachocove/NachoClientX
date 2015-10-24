#!/bin/sh

IGNORE_DIE=0
die () {
  echo "ERROR: $1"
  if [ $IGNORE_DIE -eq 0 ] ; then
      exit 1
  fi
}

TOP=$PWD
SCRIPTS=$TOP/scripts
DRY_RUN=0

build_everything() {
    if [ -z "$1" ] ; then
        die "Failed to pass a logfile"
    fi
    LOGFILE=$1
    make -f build.mk 2>&1 | tee $LOGFILE
    if [ ${PIPESTATUS[0]} -ne 0 ] ; then
        die "Failed to build auxillary packages"
    fi
}

build_nachoclient() {
    if [ -z "$1" ] ; then
        die "Failed to pass target"
    fi
    TARGET=$1

    if [ -z "$2" ] ; then
        die "No version name passed"
    fi
    VERSION=$2

    if [ -z "$3" ] ; then
        die "No build name passed"
    fi
    BUILD=$3

    if [ -z "$4" ] ; then
        die "No release name passed"
    fi
    RELEASE=$4

    if [ -z "$5" ] ; then
        die "Failed to pass a logfile"
    fi
    LOGFILE=$5

    VERSION="$VERSION" BUILD="$BUILD" RELEASE="$RELEASE" make $TARGET 2>&1 | tee -a $LOGFILE
    if [ ${PIPESTATUS[0]} -ne 0 ] ; then
        die "Build failed!"
    fi
}

fetch_branch() {
    if [ -z "$1" ] ; then
        die "Failed to pass a branch"
    fi
    $SCRIPTS/fetch.py || die "failed to fetch all repos!"
    $SCRIPTS/repos.py checkout-branch --branch $1 || die "failed to switch to branch $1"
}

create_tag() {
    if [ -z "$1" ] ; then
        die "No tag name passed"
    fi
    TAG=$1

    if [ -z "$2" ] ; then
        die "No version name passed"
    fi
    VERSION=$2

    if [ -z "$3" ] ; then
        die "No build name passed"
    fi
    BUILD=$3

    if [ ! -z "$DRY_RUN" ] ; then
        DO_DRY_RUN="--dry-run"
    else
        DO_DRY_RUN=
    fi

    $SCRIPTS/repos.py create-tag $DO_DRY_RUN --version "$VERSION" --build "$BUILD" || die "failed to tag all repos!"
    echo "Build $TAG is made."
}

fetch_tag() {
    if [ -z "$1" ] ; then
        die "No tag name passed"
    fi
    TAG=$1

    $SCRIPTS/fetch.py || die "failed to fetch all repos!"
    $SCRIPTS/repos.py checkout-tag --tag "$TAG" || die "failed to switch to tag $TAG"

    # Need to fetch and change branch again because the branch may add new repos that is not
    $SCRIPTS/fetch.py || die "failed to fetch all repos! (2)"
    $SCRIPTS/repos.py checkout-tag --tag "$TAG" || die "failed to switch to tag $TAG (2)"
}

upload_ios() {
    if [ -z "$1" ] ; then
        die "Failed to pass path to where ipas are"
    fi
    BUILD_PATH=$1

    if [ -z "$2" ] ; then
        die "No version name passed"
    fi
    VERSION=$2

    if [ -z "$3" ] ; then
        die "No build name passed"
    fi
    BUILD=$3

    if [ -z "$4" ] ; then
        die "No release name passed"
    fi
    RELEASE=$4

    if [ -z "$5" ] ; then
        NO_SKIP=
    else
        NO_SKIP="--no-skip"
    fi

    if [ ! -z "$DRY_RUN" ] ; then
        DO_DRY_RUN="--dry-run"
    else
        DO_DRY_RUN=
    fi

    VERSION="$VERSION" BUILD="$BUILD" RELEASE="$RELEASE" $SCRIPTS/hockeyapp_upload.py $DO_DRY_RUN $NO_SKIP --ios $BUILD_PATH || die "Failed to upload ipa"
}

sign_and_upload_android() {
    if [ -z "$1" ] ; then
        die "Failed to pass path to where apks are"
    fi
    BUILD_PATH=$1

    if [ -z "$2" ] ; then
        die "No version name passed"
    fi
    VERSION=$2

    if [ -z "$3" ] ; then
        die "No build name passed"
    fi
    BUILD=$3

    if [ -z "$4" ] ; then
        die "No release name passed"
    fi
    RELEASE=$4

    if [ -z "$5" ] ; then
        NO_SKIP=
    else
        NO_SKIP="--no-skip"
    fi

    set -x
    echo $PWD
    ANDROID_PACKAGE=`$SCRIPTS/projects.py $release android package_name`
    if [ -z "$ANDROID_PACKAGE" ] ; then
        echo "No package name found in projects"
        exit 1
    fi
    EXPECTED_APK="$ANDROID_PACKAGE-Signed.apk"
    RESIGNED_APK=$EXPECTED_APK-temp-resign

    if [ ! -z "$DRY_RUN" ] ; then
        DO_DRY_RUN="--dry-run"
    else
        DO_DRY_RUN=
    fi

    $SCRIPTS/android_sign.py sign --release $release --keystore-path=$HOME/.ssh $BUILD_PATH/$EXPECTED_APK $BUILD_PATH/$RESIGNED_APK || die "Failed to re-sign apk"
    mv $BUILD_PATH/$RESIGNED_APK $BUILD_PATH/$EXPECTED_APK || die "Failed to move apk"
    VERSION="$VERSION" BUILD="$BUILD" RELEASE="$RELEASE" $SCRIPTS/hockeyapp_upload.py $DO_DRY_RUN $NO_SKIP --android $BUILD_PATH || die "Failed to upload apk"
    set +x
}
