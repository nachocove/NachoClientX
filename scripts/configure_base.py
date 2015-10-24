# Copyright 2014, NachoCove, Inc
import os
import sys
from projects import projects

def setup(architecture):
    if 'RELEASE' in os.environ:
        assert 'BUILD' in os.environ and 'VERSION' in os.environ
        version = os.environ['VERSION']
        build = os.environ['BUILD']
        release = os.environ['RELEASE']
    else:
        print 'Development build'
        version = '0.1'
        build = '0'
        release = 'dev'
    if release not in projects:
        raise ValueError('Unknown release type %s' % release)
    arch_dict = projects[release][architecture]
    icon_script = arch_dict.get('icon_script', None)
    if icon_script is None:
        release_dir = None
    else:
        release_dir = os.path.dirname(icon_script)
    return (arch_dict, release, version, build, release_dir)

def copy_icons(icon_script, project_dir, release_dir):
    if icon_script is not None:
        script = os.path.basename(icon_script)
        path = '%s/%s' % (project_dir, release_dir)
        script_path = os.path.join(path, script)
        print 'Icon script = %s' % script_path
        if os.system('sh -c "cd %s; sh %s"' % (path, script)) != 0:
            return False
        return True
