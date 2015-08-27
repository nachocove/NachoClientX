#!/usr/bin/env python
# This script fetches a list of git repos concurrently. As of the creation of
# this script, it reduces the time to fetch all non-NachoClientX repo by about 1m30s
import sys
import os
import threading
import subprocess
import time
import repos_cfg


class FetchThread(threading.Thread):
    def __init__(self, repo, parent_dir):
        super(FetchThread, self).__init__()
        self.repo = repo
        self.parent_dir = parent_dir
        self._return = None

    def join(self, *args, **kwargs):
        super(FetchThread, self).join(*args, **kwargs)
        return self._return

    def run(self):
        self._return = self.nacho_run()

    def nacho_run(self):
        start = time.time()
        repo_dir = os.path.join(self.parent_dir, self.repo)
        if not os.path.exists(repo_dir):
            # git clone
            print 'Fetching %s...' % self.repo
            os.mkdir(repo_dir)
            os.chdir(self.parent_dir)
            try:
                rc = subprocess.check_call(['git', 'clone', '-q', 'git@github.com:nachocove/%s.git' % self.repo])
            except subprocess.CalledProcessError as e:
                rc = e.returncode

            if 0 == rc:
                etime = time.time() - start
                print 'Done fetching %s (%.2f sec)!' % (self.repo, etime)
            else:
                print 'ERROR: failed to fetch %s!' % self.repo
                return 1
        else:
            # git pull
            print 'Updating %s...' % self.repo
            os.chdir(repo_dir)
            try:
                rc = subprocess.check_call(['git', 'pull'])
            except subprocess.CalledProcessError as e:
                rc = e.returncode

            if 0 == rc:
                etime = time.time() - start
                print 'Done pulling %s (%.2f sec)!' % (self.repo, etime)
            else:
                print 'ERROR: failed to pull %s!' % self.repo
                return 1
        try:
            os.chdir(repo_dir)
            rc = subprocess.check_call(['git', 'submodule', 'update', '--init', '--recursive'])
        except subprocess.CalledProcessError as e:
            rc = e.returncode

        if 0 != rc:
            print 'ERROR: failed to update submodule  %s!' % self.repo
            return 1

        return 0

def main():
    if 1 == len(sys.argv):
        # If no repo is given, use the default list
        repo_list = repos_cfg.repos
    else:
        repo_list = sys.argv[1:]
    start = time.time()
    repos_dir = os.path.abspath('..')
    threads = [FetchThread(repo=repo, parent_dir=repos_dir) for repo in repo_list]
    for thread in threads:
        thread.start()

    error = False
    for thread in threads:
        rc = thread.join()
        if rc != 0:
            error = True
    print 'Total runtime: %.2f sec' % (time.time() - start)
    sys.exit(0 if not error else 1)

if __name__ == '__main__':
    main()
