#!/usr/bin/env python
# This script fetches a list of git repos concurrently. As of the creation of
# this script, it reduces the time to fetch all non-NachoClientX repo by about 1m30s
import sys
import os
import threading
import subprocess
import time
from argparse import ArgumentParser

import repos_cfg

class FetchThread(threading.Thread):
    def __init__(self, repo, parent_dir, options):
        super(FetchThread, self).__init__()
        self.repo = repo
        self.parent_dir = parent_dir
        self.stderr = []
        self.stdout = []
        self.repo_dir = os.path.join(self.parent_dir, self.repo)
        self.returncode = None
        self.options = options

    def join(self, *args, **kwargs):
        super(FetchThread, self).join(*args, **kwargs)
        return self.returncode

    def run(self):
        self.nacho_run()

    def clone (self):
        cmd = ['git', 'clone', '-q', 'git@github.com:nachocove/%s.git' % self.repo]
        return self.run_cmd(self.parent_dir, cmd, "fetching")

    def update (self):
        cmd = ['git', 'pull']
        return self.run_cmd(self.repo_dir, cmd, "updating")

    def submodule (self):
        cmd = ['git', 'submodule', 'update', '--init', '--recursive']
        return self.run_cmd(self.repo_dir, cmd, "updating submodules")

    def run_cmd (self, dir, cmd, verb):
        if not self.options.quiet or self.options.debug:
            print '%s %s...' % (verb.capitalize(), self.repo)
        start = time.time()
        os.chdir(dir)
        self.stdout.append("Running: %s" % cmd)
        repo_update = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        (error, output) = repo_update.communicate()
        if error:
            self.stderr.append(error)
        if output:
            self.stdout.append(output)
        self.returncode = repo_update.returncode
        if repo_update.returncode != 0:
            if self.options.debug:
                print 'ERROR: failed to fetch %s!' % self.repo
                print output
                print error
        etime = time.time() - start
        if not self.options.quiet or self.options.debug:
            print 'Done %s %s (%.2f sec)!' % (verb, self.repo, etime)
        return True if repo_update.returncode == 0 else False

    def nacho_run(self):
        if not os.path.exists(self.repo_dir):
            # git clone
            if not self.clone():
                return
        else:
            # git pull
            if not self.update():
                return

        if not self.submodule():
            return

def main():
    parser = ArgumentParser()
    parser.add_argument("-q", "--quiet", action="store_true", help="Less verbose output", default=False)
    parser.add_argument("-d", "--debug", action="store_true", help="More verbose output", default=False)
    parser.add_argument("repo", nargs="*", type=str, default=[])
    options = parser.parse_args()

    if not options.repo:
        # If no repo is given, use the default list
        repo_list = repos_cfg.repos
    else:
        repo_list = options.repo

    failed_repos = []

    start = time.time()
    repos_dir = os.path.abspath('..')
    threads = [FetchThread(repo=repo, parent_dir=repos_dir, options=options) for repo in repo_list]
    for thread in threads:
        thread.start()
        rc = thread.join()
        if rc != 0:
            failed_repos.append(thread)

    for thread in threads:
        rc = thread.join()
        if rc != 0:
            failed_repos.append(thread)

    if failed_repos:
        print # empty line
        for failed in failed_repos:
            print "ERROR: Update/fetch failed for: %s (ret %s)" % (failed.repo, failed.returncode)
            print "\n".join(failed.stdout)
            print "\n".join(failed.stderr)
    else:
        print "Good news, everyone! All Repos updated successfully!"

    print 'Total runtime: %.2f sec' % (time.time() - start)
    sys.exit(0 if not failed_repos else 1)

if __name__ == '__main__':
    main()
