#!/usr/bin/env python
# This script fetches a list of git repos concurrently. As of the creation of
# this script, it reduces the time to fetch all non-NachoClientX repo by about 1m30s
import sys
import os
import threading
import subprocess
import time


class FetchThread(threading.Thread):
    def __init__(self, repo, parent_dir):
        threading.Thread.__init__(self)
        self.repo = repo
        self.parent_dir = parent_dir

    def run(self):
        start = time.time()
        repo_dir = os.path.join(self.parent_dir, self.repo)
        if not os.path.exists(repo_dir):
            # git clone
            print 'Fetching %s...' % self.repo
            os.mkdir(repo_dir)
            os.chdir(self.parent_dir)
            rc = subprocess.check_call(['git', 'clone', '-q', 'git@github.com:nachocove/%s.git' % self.repo])
            if 0 == rc:
                etime = time.time() - start
                print 'Done fetching %s (%.2f sec)!' % (self.repo, etime)
            else:
                print 'ERROR: fail to fetch %s!' % self.repo
        else:
            # git pull
            print 'Updating %s...' % self.repo
            os.chdir(repo_dir)
            rc = subprocess.check_call(['git', 'pull'])
            if 0 == rc:
                etime = time.time() - start
                print 'Done pulling %s (%.2f sec)!' % (self.repo, etime)
            else:
                print 'ERROR: fail to pull %s!' % self.repo


def main():
    if 1 == len(sys.argv):
        print 'USAGE: fetch.py [repo1] [repo2] ... [repoN]'
        sys.exit(0)
    start = time.time()
    repos_dir = os.path.abspath('..')
    threads = [FetchThread(repo, repos_dir) for repo in sys.argv[1:]]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()
    print 'Total runtime: %.2f sec' % (time.time() - start)

if __name__ == '__main__':
    main()