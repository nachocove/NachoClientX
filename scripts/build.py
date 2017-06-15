#!/usr/bin/env python
import argparse
import command
import git
import plistlib


KINDS = ('store', 'alpha', 'beta')
PLATFORMS = ('ios', 'android')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('kind', help="The type of build.  e.g., store, alpha, beta")
    parser.add_argument('version', help="The version string.  e.g., 1.2.3")
    parser.add_argument('build', help="The build number.  e.g., 456")
    parser.add_argument('--config', help="Use a custom config plist rather than the one auto chosen by the kind argument")
    parser.add_argument('--ios-only', action='store_true', help="Only build iOS and nothing else")
    parser.add_argument('--android-only', action='store_true', help="Only build Android and nothing else")
    parser.add_argument('--unsigned', action='store_true', help="Only build the unsigned Android .apk")

    args = parser.parse()

    build = Build(args.kind, args.version, args.build)
    platforms = determine_platforms(args)

    builder = Builder(build, platforms=platforms, config_file=args.config, unsigned_only=args.unsigned)
    builder.execute()


def determine_platforms(args):
    if args.ios_only:
        return ('ios',)
    if args.android_only:
        return ('android',)
    return PLATFORMS


class Build(object):

    kind = None
    major = 0
    minor = 0
    bugfix = 0
    number = 0

    def __init__(self, kind, version_string, build_number):
        self.kind = kind
        parts = self.version_string.split('.')
        self.major = int(parts[0])
        if len(parts) > 1:
            self.minor = int(parts[1])
            if len(parts) > 2:
                self.bufix = int(parts[2])
        self.number = build_number

    @property
    def version_string(self):
        return '%d.%d.%d' % (self.major, self.minor, self.bugfix)

    @property
    def full_branch(self):
        return "v%d.%d.%d" % (self.major, self.minor, self.bugfix)

    @property
    def short_branch(self):
        return "v%d.%d" % (self.major, self.minor)

    @property
    def tag(self):
        if self.kind == 'store':
            return 'v%s_%s' % (self.version_string, self.build)
        return 'v%s_%s_%s' % (self.version_string, self.build, self.kind)


class Builder(object):

    build = None
    platforms = None
    ouputs = None
    config_file = None
    config = None
    unsigned_only = False

    def __init__(self, build, platforms, config_file=None, unsigned_only=False):
        self.build = build
        self.platforms = platforms
        self.config_file = config_file
        self.unsigned_only = unsigned_only
        self.load_config()
        self.outputs = []

    def nacho_path(*components):
        root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
        return os.path.join(root, *components)

    def load_config(self):
        if self.config_file is None:
            self.config_file = self.nacho_path('buildconfig', '%s.plist' % self.build.kind)
        self.config = plistlib.readPlist(self.config_file)

    def execute(self):
        self.checkout()
        if 'ios' in self.platforms:
            self.build_ios()
        if 'android' in self.platforms:
            self.build_android()
        self.tag()
        if len(self.outputs) > 0:
            print ""
            printer = repos.TablePrinter()
            printer.print_table(self.outputs)

    def checkout(self):
        # First, make sure this repo is checked out to the appropriate branch
        # Usually, the branch will be something like v3.5, containing just the major and minor version parts,
        # with each bugfix release built & tagged on that branch.  In rare cases where mutliple bugfix releases
        # are being worked on simultaneously, a v3.5.x branch can be cut, and this checkout will prefer it
        # to the v3.5 branch.
        # If neither the vX.Y or the vX.Y.Z branch is found, a vX.Y branch will be created automatiacally
        # across all repos.  This is handy because it cust the branch when doing the first alpha build all
        # in one step: build.py alpha 3.5.0 123
        import repos
        client_path = self.nacho_path()
        client_repo = repos.Repo(os.path.basename(client_path), os.path.dirname(client_path))
        branch = self.build.short_branch
        if client_repo.has_branch(self.build.full_branch):
            branch = self.build.full_branch
        print "Getting branch %s..." % branch
        if not client_repo.has_branch(branch):
            print "Creating branch %s..." % branch
            all_repos = repos.all_repos()
            for repo in all_repos:
                repo.create_branch(branch)
        client_repo.checkout(branch)

        # Next, reload supporting modules in case any have changed as a result of the checkout
        # The most likely change is repos.REPO_NAMES
        reload(command)
        reload(git)
        reload(repos)

        # Finally, checkout all other repos to the matching branch
        all_repos = repos.all_repos()
        for repo in all_repos:
            repo.checkout(branch)

    def tag(self):
        import repos
        print "Tagging as %s..." % self.build.tag
        all_repos = repos.all_repos()
        for repo in all_repos:
            repo.create_tag(self.build.tag)
            repo.push()
        self.outputs.append(('git tag:', self.build.tag))

    def build_ios():
        print "Building iOS..."
        builder = IOSBuilder(self.nacho_path('NachoClient.iOS/NachoClient.iOS.csproj'), self.build, self.config)
        builder.execute()
        self.ouputs.append(('iOS .xarchive:', builder.archive_path))
        self.ouputs.append(('iOS .ipa:', builder.ipa_path))

    def build_android():
        print "Building Android..."
        builder = AndroidBuilder(self.nacho_path('NachoClient.Android/NachoClient.Android.csproj'), self.build, self.config, self.unsigned_only)
        builder.execute()
        self.ouputs.append(('Android unsigned .apk:', builder.unsigned_apk))
        if not self.unsigned_only:
            self.ouputs.append(('Android signed .apk:', builder.signed_apk))


class IOSBuilder(object):

    build = None
    config = None
    project_path = None
    archive_path = None
    ipa_path = None

    def __init__(self, project_path, build, config):
        self.project_path = project_path
        self.build = build
        self.config = config

    def execute(self):
        self.configure()
        self.archive()
        self.export()

    def configure(self):
        self.edit_buildinfo()
        self.edit_info()
        self.edit_entitlements()
        self.edit_icons()

    def edit_buildinfo(self):
        pass

    def edit_info(self):
        pass

    def edit_entitlements(self):
        pass

    def edit_icons(self):
        pass

    def archive(self):
        cwd = os.path.dirname(self.project_path)
        cmd = command.Command('msbuild', '/t:Build', '/p:Configuration=Release', '/p:Platform=iPhone', '/p:ArchiveOnBuild=true', self.project_path, cwd=cwd)
        cmd.execute()

    def export(self):
        pass


class AndroidBuilder(object):

    build = None
    config = None
    project_path = None
    unsigned_apk = None
    signed_apk = None
    unsigned_only = None

    def __init__(self, project_path, build, config, unsigned_only):
        self.project_path = project_path
        self.build = build
        self.config = config
        self.unsigned_only = unsigned_only

    def execute(self):
        self.configure()
        self.package()
        if not self.unsigned_only:
            self.sign()

    def configure(self):
        self.edit_buildinfo()
        self.edit_manifest()
        self.edit_icons()

    def edit_buildinfo(self):
        pass

    def edit_manifest(self):
        pass

    def edit_icons(self):
        pass

    def package(self):
        cwd = os.path.dirname(self.project_path)
        cmd = command.Command('msbuild', '/t:Build', '/p:Configuration=Release', self.project_path, cwd=cwd)
        cmd.execute()

    def sign(self):
        pass


if __name__ == '__main__':
    main()
