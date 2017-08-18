#!/usr/bin/env python

import os
import os.path
import sys
import argparse
import git
import command
import build

REPO_NAMES = (
    'Reachability',
    'registered-domain-libs',
    'ios-openssl',
    'NachoPlatformBinding',
    'NachoUIMonitorBinding',
    'MailKit',
    'DnDns',
    'DDay-iCal-Xamarin',
    'CSharp-Name-Parser',
    'lucene.net-3.0.3',
    'MobileHtmlAgilityPack',
    'TokenAutoComplete',
    'TokenAutoCompleteBinding',
    'rtfparserkit',
    'rtfparserkitBinding',
    'OkHttp-Xamarin',
    # # This is always the last one
    'NachoClientX'
)

BRANCH_EXCEPTIONS = {
}


class TablePrinter(object):

    def __init__(self):
        pass

    def print_table(self, rows):
        if len(rows) == 0:
            return
        colcount = len(rows[0])
        minspacing = 2
        colwidth = [0] * colcount
        for row in rows:
            for col in range(colcount):
                colwidth[col] = max((len(unicode(row[col])) + minspacing) if row[col] is not None and len(unicode(row[col])) > 0 else 0, colwidth[col])
        for row in rows:
            cols = []
            for col in range(colcount):
                fmt = "%%-%ds" % colwidth[col]
                cols.append(fmt % (unicode(row[col]) if row[col] is not None else ''))
            print ''.join(cols)


class Repo(object):

    name = None
    path = None
    branches = None
    branch = None
    status = None
    fixed_branch = None

    def __init__(self, path):
        self.name = os.path.basename(path)
        self.path = path
        self.fixed_branch = BRANCH_EXCEPTIONS.get(self.name, dict()).get('fixed-branch', None)
        if not os.path.exists(self.path):
            self.clone()
        (self.branches, self.branch) = git.list_branches(cwd=self.path)

    def clone(self):
        url = 'git@github.com:nachocove/%s.git' % self.name
        print "Cloning %s..." % url
        git.clone(url, self.path)
        git.submodule_init(cwd=self.path)

    def checkout(self, branch):
        if self.fixed_branch is not None:
            return
        git.checkout(branch, cwd=self.path)
        git.submodule_update()
        self.branch = branch

    def pull(self):
        git.pull(cwd=self.path)
        git.submodule_update()

    def push(self, branch_or_tag=None):
        git.push(self.branch if branch_or_tag is None else branch_or_tag, cwd=self.path)

    def create_tag(self, tag, message=None):
        git.create_tag(tag, message, cwd=self.path)

    def delete_tag(self, tag):
        git.delete_tag(tag, cwd=self.path)

    def create_branch(self, branch):
        if self.fixed_branch is not None:
            return
        if self.has_branch(branch):
            return
        git.create_branch(branch, cwd=self.path)
        git.submodule_update()
        self.branch = branch
        self.branches.append(branch)

    def delete_branch(self, branch):
        if self.fixed_branch is not None:
            return
        if self.has_branch(branch):
            return
        git.delete_branch(branch, cwd=self.path)
        self.branches.remove(branch)

    def query_status(self):
        self.status = git.status(cwd=self.path)

    @property
    def status_symbol(self):
        if self.status is None:
            self.query_status()
        if self.status.has_changes:
            if self.status.has_untracked:
                return '?M'
            else:
                return 'M'
        elif self.status.has_untracked:
            return '?'
        else:
            return ''

    def has_branch(self, branch):
        return (branch in self.branches) or (('origin/%s' % branch) in self.branches)


def setup_argparser():
    main_parser = argparse.ArgumentParser()
    cmd_parser = main_parser.add_subparsers(dest='command', help='commands')

    parser = cmd_parser.add_parser('branch', help='List the current branch of all repositories', description='List the current branch of all repositories.')
    parser.set_defaults(func=command_branch)
    
    # checkout branch
    parser = cmd_parser.add_parser('checkout-branch', help='Switch to an existing branch', description='Switch to an existing branch')
    parser.add_argument('--branch', type=str, help='Branch name')
    parser.set_defaults(func=command_checkout_branch)

    #checkout tag
    parser = cmd_parser.add_parser('checkout-tag', help='Switch to a tagged snapshot',description='Switch to a tagged snapshot. Note that all local repos will be in a detached state.')
    parser.add_argument('--tag', type=str, help='Tag name')
    parser.add_argument('--build', type=str, default=None, help='Build number')
    parser.add_argument('--version', type=str, default=None, help='Build version')
    parser.set_defaults(func=command_checkout_tag)

    #create branch
    parser = cmd_parser.add_parser('create-branch', help='Create a branch from a given name', description='Create a branch from a given name. To create a branch with an arbitrary name, use --branch.')
    parser.add_argument('--branch', type=str, help='Branch name')
    parser.set_defaults(func=command_create_branch)

    #create tag
    parser = cmd_parser.add_parser('create-tag', help='Create a tag from a given name, or build', description='Create a tag from a given name, or build. To create a tag with an arbitrary name, use --tag. To create a tag from a build, use --version and --build. The tag name is "v<VERSION>_<BUILD>".')
    parser.add_argument('--tag', type=str, help='Tag name')
    parser.add_argument('--kind', type=str, default=None, help='What kind of build', choices=build.KINDS)
    parser.add_argument('--build', type=str, default=None, help='Build number')
    parser.add_argument('--version', type=str, default=None, help='Build version')
    parser.set_defaults(func=command_create_tag)

    #delete branch
    parser = cmd_parser.add_parser('delete-branch', help='Delete a branch from a given name', description='Delete a branch from a given name.')
    parser.add_argument('--branch', type=str, help='Branch name')
    parser.set_defaults(func=command_delete_branch)

    #delete tag
    parser = cmd_parser.add_parser('delete-tag', help='Delete a tag from a given name', description='Delete a tag from a given name.')
    parser.add_argument('--tag', type=str, help='Tag name')
    parser.set_defaults(func=command_delete_tag)

    #status
    parser = cmd_parser.add_parser('status', help='Return git status for all repositories', description='Return git status for all repositories.')
    parser.add_argument('--details', action='store_true', help='Show changed file lists for each repository with changes')
    parser.set_defaults(func=command_status)

    #push
    parser = cmd_parser.add_parser('push', help='push all repositories to remote origin', description='push all repositories.')
    parser.add_argument('--tag')
    parser.set_defaults(func=command_push)

    #pull
    parser = cmd_parser.add_parser('pull', help='pull all repositories from remote origin', description='update all repositories.')
    parser.set_defaults(func=command_pull)

    #clone
    parser = cmd_parser.add_parser('clone', help='ensure all repositories are present', description='ensure all repositories are present.')
    parser.set_defaults(func=command_clone)

    return main_parser


def command_branch(args):
    repos = all_repos()
    print_status(repos)


def command_checkout_branch(args):
    repos = all_repos()
    for repo in repos:
        print "Checking out %s..." % repo.name
        repo.checkout(args.branch)
    print ""
    print_status(repos)


def command_checkout_tag(args):
    tag = None
    if args.tag:
        tag = args.tag
    elif args.version and args.build and args.kind:
        tag = build.Build(args.kind, args.version, args.build).tag
    else:
        print 'ERROR: must have --tag OR --kind, --version, and --build specified'
        sys.exit(1)
    repos = all_repos()
    for repo in repos:
        print "Checking out %s..." % repo.name
        repo.checkout(branch)
    print ""
    print_status(repos)


def command_create_branch(args):
    repos = all_repos()
    for repo in repos:
        print "Creating branch %s with %s..." % (branch, repo.name)
        repo.create_branch(branch)
    print ""
    print_status(repos)


def command_create_tag(args):
    tag = None
    if args.tag:
        tag = args.tag
    elif args.version and args.build and args.kind:
        tag = build.Build(args.kind, args.version, args.build).tag
    else:
        print 'ERROR: must have --tag OR --kind, --version, and --build specified'
        sys.exit(1)
    repos = all_repos()
    for repo in repos:
        print "Creating tag %s on %s..." % (tag, repo.name)
        repo.create_tag(tag)
    print "\nDone"


def command_delete_branch(args):
    repos = all_repos()
    for repo in repos:
        print "Removing branch %s from %s..." % (branch, repo.name)
        repo.delete_branch(branch)
    print ""
    print_status(repos)


def command_delete_tag(args):
    repos = all_repos()
    for repo in repos:
        print "Deleting tag %s with %s..." % (args.tag, repo.name)
        repo.delete_tag(args.tag)
    print "\nDone"


def command_status(args):
    repos = all_repos()
    for repo in repos:
        print "Querying status of %s..." % repo.name
        repo.query_status()
    print ""
    print_status(repos)
    if args.details:
        for repo in repos:
            if repo.status.has_untracked or repo.status.has_changes:
                print "\n%s" % repo.name
                repo.status.print_status()


def command_pull(args):
    repos = all_repos()
    for repo in repos:
        print "Pulling %s..." % repo.name
        repo.pull()
    print ""
    print_status(repos)


def command_push(args):
    repos = all_repos()
    for repo in repos:
        print "Pushing %s..." % repo.name
        repo.push(args.tag)
    print "\nDone"


def command_clone(args):
    top = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
    repos = []
    for name in REPO_NAMES:
        print "Cloning %s..." % name
        repos.append(Repo(os.path.join(top, name)))
    print ""
    print_status(repos)


def print_status(repos):
    printer = TablePrinter()
    printer.print_table([(repo.status_symbol, repo.name, repo.branch) for repo in repos])


def all_repos():
    top = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
    repos = []
    for name in REPO_NAMES:
        repos.append(Repo(os.path.join(top, name)))
    return repos


def main():
    parser = setup_argparser()
    args = parser.parse_args()
    try:
        args.func(args)
    except command.CommandError as e:
        print 'Error: '.join(e.cmd.cmd)
        print e.cmd.stderr
        sys.exit(1)


if __name__ == '__main__':
    main()
