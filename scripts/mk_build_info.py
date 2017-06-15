#!/usr/bin/env python

# This script creates a developer BuildInfo.cs that contains information about build time / setting.

from argparse import ArgumentParser
import build


def main():
    parser = ArgumentParser()
    parser.add_argument('platform', choices=build.PLATFORMS, help='Which platform are we building?')
    parser.add_argument('--config', help='Use this config file instead of the default dev config')
    args = parser.parse_args()

    build = build.DevBuild()
    build.source = git.source_line(cwd=nacho_path())
    config = load_config(config_file=args.config)
    
    if args.platform == 'ios':
        build_ios(build, config)
    elif args.platform == 'andriod':
        build_android(build, config)


def load_config(config_file=None):
    if config_file = None:
        config_file = nacho_path('buildconfig', 'dev.plist')
    config = build.BuildConfig(config_file)


def nacho_path(*components):
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    return os.path.join(root, *components)


def build_ios(build, config):
    builder = build.IOSBuilder(nacho_path("NachoClient.iOS/NachoClient.iOS.csproj", build, config))
    builder.edit_buildinfo()


def build_android(build, config):
    builder = build.AndroidBuilder(nacho_path("NachoClient.Android/NachoClient.Android.csproj", build, config))
    builder.edit_buildinfo()


if __name__ == '__main__':
    main()
