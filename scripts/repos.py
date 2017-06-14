#!/usr/bin/env python

import os
import os.path
import sys
from argparse import ArgumentParser
import git
import command

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
    'aws-sdk-xamarin',
    'lucene.net-3.0.3',
    'MobileHtmlAgilityPack',
    'JetBlack.Caching',
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


class Build(object):
    def __init__(self, version, build):
        self.version = version
        self.build = build

    def tag(self):
        return 'v%s_%s' % (self.version, self.build)


class TablePrinter(object):

    def __init__(self):
        pass

    def print_table(self, rows):
        if len(rows) == 0:
            return
        colcount = len(rows[0])
        minspacing = 2
        colwidth = [minspacing] * colcount
        for row in rows:
            for col in range(colcount):
                colwidth[col] = max(len(unicode(row[col]) if row[col] is not None else '') + minspacing, colwidth[col])
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

    def __init__(self, name, path):
        self.name = name
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
        git.update(cwd=self.path)
        git.submodule_update()

    def push(self):
        git.push(self.branch, cwd=self.path)

    def create_tag(self, tag, message=None):
        git.create_tag(tag, message, cwd=self.path)

    def delete_tag(self, tag):
        git.delete_tag(tag, cwd=self.path)

    def create_branch(self, branch):
        if self.fixed_branch is not None:
            return
        git.create_branch(branch, cwd=self.path)
        git.submodule_update()
        self.branch = branch
        self.branches.append(branch)

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


class RepoGroup(object):

    repos = None
    client_repo = None

    def __init__(self, top=None):
        self.top = top
        self.repos = []
        for name in REPO_NAMES:
            self.repos.append(Repo(name, self.repo_dir(name)))
        self.client_repo = self.repos[-1]

    def repo_dir(self, name):
        return os.path.join(self.top, name)

    def print_status(self):
        printer = TablePrinter()
        printer.print_table([(repo.status_symbol, repo.name, repo.branch) for repo in self.repos])

    def checkout_branch(self, branch):
        for repo in self.repos:
            print "Checking out %s..." % repo.name
            repo.checkout(branch)
        print ""
        self.print_status()

    def pull(self):
        for repo in self.repos:
            print "Pulling %s..." % repo.name
            repo.pull()
        print ""
        self.print_status()

    def push(self):
        for repo in self.repos:
            print "Pusing %s..." % repo.name
            repo.push()
        print "\nDone"

    def create_tag(self, tag, message=None):
        for repo in self.repos:
            print "Creating tag %s with %s..." % (tag, repo.name)
            repo.tag(tag, message)
        print "\nDone"

    def delete_tag(self, tag):
        for repo in self.repos:
            print "Deleting tag %s with %s..." % (tag, repo.name)
            repo.tag(tag, message)
        print "\nDone"

    def create_branch(self, branch):
        for repo in self.repos:
            print "Creating branch %s with %s..." % (branch, repo.name)
            repo.create_branch(branch)
        print ""
        self.print_status()

    def delete_branch(self, branch):
        for repo in self.repos:
            print "Removing branch %s from %s..." % (branch, repo.name)
            repo.delete_branch(branch)
        print ""
        self.print_status()

    def get_current_branch(self):
        self.print_status()

    def get_status(self, details=False):
        for repo in self.repos:
            print "Querying status of %s..." % repo.name
            repo.query_status()
        print ""
        self.print_status()

        if details:
            for repo in self.repos:
                if repo.status.has_untracked or repo.status.has_changes:
                    print "\n%s" % repo.name
                    repo.status.print_status()


def setup_argparser():
    main_parser = ArgumentParser()
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
    parser.set_defaults(func=command_push)

    #pull
    parser = cmd_parser.add_parser('pull', help='pull all repositories from remote origin', description='update all repositories.')
    parser.set_defaults(func=command_pull)

    return main_parser


def find_top():
    def is_top(d):
        return 'NachoClientX' in os.listdir(d)
    cur_dir = os.getcwd()
    top = None
    if is_top(cur_dir):
        top = cur_dir
    else:
        while '/' != cur_dir:
            cur_dir = os.path.dirname(cur_dir)
            if is_top(cur_dir):
                top = cur_dir
                break
    return top


def command_branch(args, repos):
    repos.get_current_branch()


def command_checkout_branch(args, repos):
    repos.checkout_branch(args.branch)


def command_checkout_tag(args, repos):
    tag = None
    if args.tag:
        tag = args.tag
    elif args.version and args.build:
        tag = Build(version=args.version, build=args.build).tag()
    else:
        print 'ERROR: must have both --version and --build specified'
        sys.exit(1)
    repos.checkout_branch(tag)


def command_create_branch(args, repos):
    repos.create_branch(args.branch)


def command_create_tag(args, repos):
    tag = None
    if args.tag:
        tag = args.tag
    elif args.version and args.build:
        tag = Build(version=args.version, build=args.build).tag()
    else:
        print 'ERROR: must have both --version and --build specified'
        sys.exit(1)
    repos.create_branch(tag)


def command_delete_branch(args, repos):
    repos.delete_branch(args.branch)


def command_delete_tag(args, repos):
    repos.delete_tag(args.tag)


def command_status(args, repos):
    repos.get_status(details=args.details)


def command_pull(args, repos):
    repos.pull()


def command_push(args, repos):
    repos.push()


def main():
    top = find_top()
    if top is None:
        print 'ERROR: cannot find a suitable top-level directory'
        sys.exit(1)

    parser = setup_argparser()
    repo_group = RepoGroup(top)

    args = parser.parse_args()

    try:
        args.func(args, repo_group)
    except command.CommandError as e:
        print 'Error: '.join(e.cmd.cmd)
        print e.cmd.stderr
        sys.exit(1)


if __name__ == '__main__':
    main()
