# I looked into GitPython and other python module for manipulating git.
# In the end, I decide to go with a simpler approach of just sending commands
# and parsing the result myself. The reason is that: 1) we do not need
# a full set of commands (that GitPython provided), 2) I want to avoid
# any user installing another non-standard module.

import datetime
from command import Command


class GitCommand(Command):
    def __init__(self, params):
        super(GitCommand, self).__init__(['git'] + params)
        self.execute()

    def __str__(self):
        return "%s" % " ".join(self.cmd)



class BranchCommand(GitCommand):
    def __init__(self, params=None):
        if params is None:
            params = []
        super(BranchCommand, self).__init__(['branch'] + params)


class ListBranches(BranchCommand):
    def __init__(self):
        super(ListBranches, self).__init__()
        self.branches = []
        self.currnet_branch = None
        if self.ran_ok():
            # Parse the output
            for line in self.stdout.split('\n'):
                if len(line) == 0:
                    continue
                this_branch = line[2:]
                self.branches.append(this_branch)
                if line[0] == '*':
                    self.current_branch = this_branch


class CreateBranch(BranchCommand):
    def __init__(self, branch_name):
        super(CreateBranch, self).__init__([branch_name])


class DeleteBranch(BranchCommand):
    def __init__(self, branch_name):
        super(DeleteBranch, self).__init__(['-D', branch_name])


class Status(GitCommand):
    def __init__(self):
        super(Status, self).__init__(['status', '--porcelain'])
        self.index_files = {}
        self.files = {}
        self.untracked_files = []
        if self.return_code == 0:
            # Parse the output
            for line in self.stdout.split('\n'):
                if len(line) == 0:
                    continue
                (this_status, this_file) = (line[:2], line[3:])
                # Determine the status
                if this_status == '??':
                    self.untracked_files.append(this_file)
                elif this_status[0] != ' ':
                    self.index_files[this_file] = this_status[0]
                elif this_status[1] != ' ':
                    self.files[this_file] = this_status[1]
                else:
                    raise ValueError('unknown status "%s"' % this_status)

    def is_pristine(self):
        return self.has_no_change() and len(self.untracked_files) == 0

    def has_no_change(self):
        return len(self.index_files) == 0 and len(self.files) == 0


class Checkout(GitCommand):
    def __init__(self, branch_name):
        super(Checkout, self).__init__(['checkout', '-q', branch_name])

class Update(GitCommand):
    def __init__(self):
        super(Update, self).__init__(['pull', '--all', '--prune'])

class SubModuleUpdate(GitCommand):
    def __init__(self):
        super(SubModuleUpdate, self).__init__(['submodule', 'update', '--recursive'])

class TagCommand(GitCommand):
    def __init__(self, params):
        super(TagCommand, self).__init__(['tag'] + params)


class CreateTag(TagCommand):
    def __init__(self, tag, message=None):
        if message is None:
            message = 'Created at %s' % str(datetime.datetime.now())
        super(CreateTag, self).__init__(['-a', tag, '-m', message])


class DeleteTag(TagCommand):
    def __init__(self, tag):
        super(DeleteTag, self).__init__(['-d', tag])


class Push(GitCommand):
    def __init__(self, branch_name):
        super(Push, self).__init__(['push', 'origin', branch_name])