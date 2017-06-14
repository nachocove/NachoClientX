# I looked into GitPython and other python module for manipulating git.
# In the end, I decide to go with a simpler approach of just sending commands
# and parsing the result myself. The reason is that: 1) we do not need
# a full set of commands (that GitPython provided), 2) I want to avoid
# any user installing another non-standard module.

import datetime
import os.path
from command import Command


def clone(url, dest):
    cwd = os.path.dirname(dest)
    name = os.path.basename(dest)
    cmd = Command('git', 'clone', url, name, cwd=cwd)
    cmd.execute()


def status(cwd=None):
    cmd = Command('git', 'status', '--porcelain', cwd=cwd)
    cmd.execute()
    return Status(cmd.stdout)


def checkout(branch, cwd=None):
    cmd = Command('git', 'checkout', '-q', branch, cwd=cwd)
    cmd.execute()


def list_branches(cwd=None):
    cmd = Command('git', 'branch', cwd=cwd)
    cmd.execute()
    lines = cmd.stdout.split("\n")
    branches = []
    current_branch = None
    for line in lines:
        if len(line) > 0:
            branch = line[2:]
            branches.append(branch)
            if line[0] == '*':
                current_branch = branch
    return (branches, current_branch)


def create_branch(branch, cwd=None):
    cmd = Command('git', 'checkout', '-b', branch, cwd=cwd)
    cmd.execute()


def delete_branch(branch, cwd=None):
    cmd = Command('git', 'branch', '-D', branch, cwd=cwd)
    cmd.execute()


def create_tag(tag, message=None, cwd=None):
    if message is None:
        message = 'Created at %s' % str(datetime.datetime.now())
    cmd = Command('git', 'tag', '-a', tag, '-m', message, cwd=cwd)
    cmd.execute()


def delete_tag(tag, cwd=None):
    cmd = Command('git', 'tag', '-d', tag, cwd=cwd)
    cmd.execute()


def pull(cwd=None):
    cmd = Command('git', 'pull', '--all', '--prune', cwd=cwd)
    cmd.execute()


def push(branch, cwd=None):
    cmd = Command('git', 'push', 'origin', branch, cwd=cwd)
    cmd.execute()


def submodule_update(cwd=None):
    cmd = Command('git', 'submodule', 'update', '--recursive', cwd=cwd)
    cmd.execute()


def submodule_init(cwd=None):
    cmd = Command('git', 'submodule', 'update', '--init', '--recursive', cwd=cwd)
    cmd.execute()


class Status(object):

    index_files = None
    files = None
    untracked_files = None

    def __init__(self, output):
        self.index_files = dict()
        self.files = dict()
        self.untracked_files = []
        for line in output.split("\n"):
            if len(line) > 0:
                (status, file) = (line[:2], line[3:])
                # Determine the status
                if status == '??':
                    self.untracked_files.append(file)
                elif status[0] != ' ':
                    self.index_files[file] = status[0]
                elif status[1] != ' ':
                    self.files[file] = status[1]
                else:
                    raise ValueError('unknown status "%s"' % status)

    @property
    def has_untracked(self):
        return len(self.untracked_files) > 0

    @property
    def has_changes(self):
        return len(self.index_files) > 0 or len(self.files) > 0

    def print_status(self):
        if len(self.index_files) > 0:
            print "  Indexed:"
            for file in self.index_files:
                print "    %s" % file
        if len(self.files) > 0:
            print "  Unindexed:"
            for file in self.files:
                print "    %s" % file
        if len(self.untracked_files) > 0:
            print "  Untracked:"
            for file in self.untracked_files:
                print "    %s" % file
