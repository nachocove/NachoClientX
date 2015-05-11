#!/usr/bin/env python

import os
import sys
from argparse import ArgumentParser
import git
import repos_cfg


class Build:
    def __init__(self, version, build):
        self.version = version
        self.build = build

    def tag(self):
        return 'v%s_%s' % (self.version, self.build)

    def branch(self):
        return 'branch_' + self.tag()


class RepoGroup:
    def __init__(self, top=None):
        self.top = top

    def repo_dir(self, repo):
        return os.path.join(self.top, repo)

    def checkout_branch(self, branch):
        ok = True
        for repo in repos_cfg.repos:
            try:
                os.chdir(self.repo_dir(repo))

                # Check for exception
                real_branch = branch
                if repo in repos_cfg.branch_exceptions:
                    fixed_branch = repos_cfg.branch_exceptions[repo].get('fixed-branch', None)
                    if fixed_branch is not None:
                        real_branch = fixed_branch

                checkout_cmd = git.Checkout(real_branch)
                if checkout_cmd.ran_ok():
                    print '%s -> %s' % (repo, real_branch)
                else:
                    print '%s -> ERROR!' % repo
                    print checkout_cmd.stderr
                    ok = False
            except OSError:
                print '%s -> FAILED!' % repo
                ok = False
        return ok

    def create_tag(self, tag, message=None):
        ok = True
        for repo in repos_cfg.repos:
            try:
                os.chdir(self.repo_dir(repo))
                tag_cmd = git.CreateTag(tag, message)
                if tag_cmd.ran_ok():
                    push_cmd = git.Push(tag)
                    if push_cmd.ran_ok():
                        print '%s -> OK' % repo
                    else:
                        print '%s -> push failed!' % repo
                        print push_cmd.stderr
                else:
                    print '%s -> tag failed!' % repo
                    print tag_cmd.stderr
                    ok = False
            except OSError:
                print '%s -> FAILED!' % repo
                ok = False
        return ok

    def create_branch(self, branch):
        ok = True
        for repo in repos_cfg.repos:
            try:
                os.chdir(self.repo_dir(repo))
                branch_cmd = git.CreateBranch(branch)
                if branch_cmd.ran_ok():
                    push_cmd = git.Push(branch)
                    if push_cmd.ran_ok():
                        print '%s -> OK' % repo
                    else:
                        print '%s -> push failed!' % repo
                        print push_cmd.stderr
                else:
                    print '%s -> create branch failed!' % repo
                    print branch_cmd.stderr
                    ok = False
            except OSError:
                print '%s -> FAILED!' % repo
                ok = False
        return ok

    def get_current_branch(self):
        ok = True
        fmt = '%%-%ds %%s' % (max([len(x) for x in repos_cfg.repos])+3)
        for repo in repos_cfg.repos:
            try:
                os.chdir(self.repo_dir(repo))
                cmd = git.ListBranches()
                if cmd.ran_ok():
                    print fmt % (repo, cmd.current_branch)
                else:
                    print fmt % (repo, 'ERROR!')
                    print cmd.stderr
                    ok = False
            except OSError:
                print fmt % (repo, 'FAILED!')
                ok = False
        return ok

    def get_status(self, is_brief=False):
        ok = True
        repo_status = dict()
        for repo in repos_cfg.repos:
            repo_status[repo] = {'branch': '', 'status': ''}
            try:
                os.chdir(self.repo_dir(repo))
                branch_cmd = git.ListBranches()
                if branch_cmd.ran_ok():
                    repo_status[repo]['branch'] = branch_cmd.current_branch
                else:
                    repo_status[repo]['branch'] = 'ERROR!'
                    ok = False
                    continue
                status_cmd = git.Status()
                if status_cmd.ran_ok():
                    if is_brief:
                        repo_status[repo]['status'] = 'unindexed=%d, indexed=%d, untracked=%d' % \
                                                      (len(status_cmd.files),
                                                       len(status_cmd.index_files),
                                                       len(status_cmd.untracked_files))
                    else:
                        repo_status[repo]['status'] = status_cmd.stdout
                else:
                    ok = False
                    if is_brief:
                        repo_status[repo]['status'] = 'ERROR!'
                    else:
                        repo_status[repo]['status'] = status_cmd.stderr
            except OSError:
                repo_status[repo]['branch'] = 'FAILED!'
                ok = False

        # Now format everything
        for repo in repos_cfg.repos:
            if is_brief:
                fmt = '%%-%ds  %%-%ds %%s' % (max([len(r) for r in repos_cfg.repos])+3,
                                              max([len(s['branch']) for s in repo_status.values()])+3)
                print fmt % (repo, repo_status[repo]['branch'], repo_status[repo]['status'])
            else:
                print '[' + repo + '] ' + ('-' * (70 - len(repo)))
                print repo_status[repo]['status']

        return ok


def main():
    parser = ArgumentParser()
    subparser = parser.add_subparsers(dest='command', help='commands')

    subparser.add_parser('branch',
                         help='List the current branch of all repositories',
                         description='List the current branch of all repositories.')

    def add_label_or_build(parent, label, desc):
        parent.add_argument(label, type=str, help=desc)
        parent.add_argument('--build', type=str, default=None, help='Build number')
        parent.add_argument('--version', type=str, default=None, help='Build version')

    checkout_branch_parser = subparser.add_parser('checkout-branch',
                                                  help='Switch to an existing branch',
                                                  description='Switch to an existing branch')
    add_label_or_build(checkout_branch_parser, '--branch', 'Branch name')

    checkout_tag_parser = subparser.add_parser('checkout-tag',
                                               help='Switch to a tagged snapshot',
                                               description='Switch to a tagged snapshot. Note that all local'
                                                           ' repos will be in a detached state.')
    add_label_or_build(checkout_tag_parser, '--tag', 'Tag name')

    create_branch_parser = subparser.add_parser('create-branch',
                                                help='Create a branch from a given name, tag, or build',
                                                description='Create a branch from a given name, tag, or build. '
                                                            'To create a branch with an arbitrary name, use --branch. '
                                                            'To create a branch from a tag, use --branch. The branch '
                                                            'name will be the tag name prefixed by "branch_". To '
                                                            'create a branch from a build, use --version and --build. '
                                                            'The branch name is "branch_v<VERSION>_<BUILD>".')
    add_label_or_build(create_branch_parser, '--branch', 'Branch name')
    create_branch_parser.add_argument('--tag', type=str, default=None, help='Tag name')

    create_tag_parser = subparser.add_parser('create-tag',
                                             help='Create a tag from a given name, or build',
                                             description='Create a tag from a given name, or build. To create '
                                                         'a tag with an arbitrary name, use --tag. To create '
                                                         'a tag from a build, use --version and --build. The tag '
                                                         'name is "v<VERSION>_<BUILD>".')
    add_label_or_build(create_tag_parser, '--tag', 'Tag name')

    status_parser = subparser.add_parser('status',
                                         help='Return git status for all repositories',
                                         description='Return git status for all repositories. Use --brief to get '
                                                     'a summary table. Without it, it returns the git status '
                                                     'output for all repositories.')
    status_parser.add_argument('--brief', action='store_true', help='One line per repo format')

    # Determine the top directory
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
    if top is None:
        print 'ERROR: cannot find a suitable top-level directory'
        sys.exit(1)

    repo_group = RepoGroup(top)

    def check_build_params(opt):
        if not opt.version or not opt.build:
            print 'ERROR: must have both --version and --build specified'
            sys.exit(1)

    def get_branch(opt):
        if opt.branch:
            return opt.branch
        check_build_params(opt)
        return Build(version=opt.version, build=opt.build).branch()

    def get_tag(opt):
        if opt.tag:
            return opt.tag
        check_build_params(opt)
        return Build(version=opt.version, build=opt.build).tag()

    options = parser.parse_args()
    if options.command == 'branch':
        repo_group.get_current_branch()
    elif options.command == 'checkout-branch':
        repo_group.checkout_branch(get_branch(options))
    elif options.command == 'checkout-tag':
        repo_group.checkout_branch(get_tag(options))
    elif options.command == 'create-branch':
        branch = options.branch
        tag = None
        if options.tag:
            tag = options.tag
            branch = 'branch_' + tag
        elif options.build and options.version:
            build = Build(version=options.version, build=options.build)
            branch = build.branch()
            tag = build.tag()
        if tag is not None:
            # Need to make a branch out of a tag
            if not repo_group.checkout_branch(tag):
                print 'ERROR: fail to check out the required tag for branch creation (%s)' % tag
                sys.exit(1)
        if branch is None:
            print 'ERROR: no branch name specified'
            create_branch_parser.print_usage()
            sys.exit(1)
        repo_group.create_branch(branch)
    elif options.command == 'create-tag':
        repo_group.create_tag(get_tag(options))
    elif options.command == 'status':
        repo_group.get_status(options.brief)

if __name__ == '__main__':
    main()
