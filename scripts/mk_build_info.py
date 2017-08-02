#!/usr/bin/env python

# This script creates a developer BuildInfo.cs that contains information about build time / setting.

from argparse import ArgumentParser
import build
import git
import os.path


def main():
    parser = ArgumentParser()
    parser.add_argument('platform', choices=build.PLATFORMS, help='Which platform are we building?')
    parser.add_argument('--config', help='Use this config file instead of the default dev config')
    args = parser.parse_args()

    buildspec = build.DevBuild()
    buildspec.source = git.source_line(cwd=nacho_path())
    config = load_config(config_file=args.config)
    
    if args.platform == 'ios':
        build_ios(buildspec, config)
    elif args.platform == 'android':
        build_android(buildspec, config)


def load_config(config_file=None):
    if config_file is None:
        config_file = nacho_path('buildconfig', 'dev.plist')
    config = build.BuildConfig(config_file)
    return config


def nacho_path(*components):
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    return os.path.join(root, *components)


def build_ios(buildspec, config):
    builder = build.IOSBuilder(nacho_path("NachoClient.sln"), "NachoClient.iOS", buildspec, config)
    builder.edit_buildinfo()
    builder.edit_callerid_buildinfo()
    builder.edit_share_buildinfo()


def build_android(buildspec, config):
    builder = build.AndroidBuilder(nacho_path("NachoClient.sln"), "NachoClient.Android", buildspec, config)
    builder.edit_buildinfo()


if __name__ == '__main__':
    main()
