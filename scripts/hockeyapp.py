import subprocess
import json


class HockeyAppError(Exception):
    def __init__(self, desc):
        self.desc = desc

    def __str__(self):
        return str(self.desc)


class CurlCommand:
    """
    Simple wrapper around curl command. Instead of writing our own
    Python-based URL driver, we'll just use curl since it does the same
    thing with much less work. curl supports multipart/form-data better
    than urllib.
    """
    def __init__(self, verbose=False):
        self.verbose = verbose
        if self.verbose:
            self.params = ['-v']
        else:
            self.params = ['--silent']
        self.url_ = None

    def form_data(self, field, value):
        """
        Wrap -F
        """
        self.params.extend(['-F', '%s=%s' % (field, value)])
        return self

    def header(self, field, value):
        """
        Wrap -H
        """
        self.params.extend(['-H', '%s: %s' % (field, value)])
        return self

    def url(self, url_):
        self.url_ = url_
        return self

    def put(self):
        self.params.extend(['-X', 'PUT'])
        return self

    def get(self):
        self.params.extend(['-X', 'GET'])
        return self

    def post(self):
        self.params.extend(['-X', 'POST'])
        return self

    def delete(self):
        self.params.extend(['-X', 'DELETE'])
        return self

    def _command(self):
        return ['curl'] + self.params + [self.url_]

    def __str__(self):
        def quotify(s):
            if ' ' in s:
                return '"%s"' % s
            return s
        return ' '.join([quotify(x) for x in self._command()])

    def run(self):
        if self.url is None:
            raise ValueError('URL is not set')

        output = subprocess.check_output(self._command())
        try:
            return json.loads(output)
        except ValueError:
            return {}


class HockeyApp:
    def __init__(self, api_token):
        self.api_token = api_token
        self.base_url = 'https://rink.hockeyapp.net/api/2'

    def base_command(self):
        return CurlCommand().header('X-HockeyAppToken', self.api_token)

    def command(self, url_, form_data=None):
        cmd = self.base_command()
        if isinstance(form_data, dict):
            for (field, value) in form_data.items():
                cmd.form_data(field, value)
        cmd.url(url_)
        return cmd

    def url(self, path=None):
        if path is None:
            return self.base_url
        return self.base_url + path

    def apps(self):
        app_list = []
        response = self.command(self.base_url + '/apps').get().run()
        if response['status'] != 'success':
            raise ValueError('Server returns failures (status=%s)' % response['status'])
        for app_data in response['apps']:
            app = App(hockeyapp_obj=self,
                      app_id=str(app_data['public_identifier']),
                      title=app_data['title'],
                      bundle_id=str(app_data['bundle_identifier']),
                      platform=str(app_data['platform']))
            app.release_type = App.release_type_from_value(app_data['release_type'])
            app_list.append(app)
        return app_list

    def app(self, app_id):
        return App(hockeyapp_obj=self, app_id=app_id).read()

    def delete_app(self, app_id):
        app = App(hockeyapp_obj=self, app_id=app_id)
        app.delete()


class App:
    RELEASE_TYPES = {'beta': 0,
                     'live': 1,
                     'alpha': 2}

    PLATFORMS = ('iOS', 'Android', 'Mac OS', 'Windows Phone')

    @staticmethod
    def check_release_type(release_type):
        if release_type is not None and release_type not in App.RELEASE_TYPES:
            raise ValueError('Invalid release type. Choices are: ' + ' '.join(App.RELEASE_TYPES.keys()))

    @staticmethod
    def check_platform(platform):
        if platform is not None and platform not in App.PLATFORMS:
            raise ValueError('Invalid platform type. Choices are: ' + ' '.join(App.PLATFORMS))

    @staticmethod
    def release_type_from_value(release_type_value):
        for (type_, value) in App.RELEASE_TYPES.items():
            if value != release_type_value:
                continue
            return type_
        else:
            raise ValueError('unknown release type value %s' % str(release_type_value))

    def __init__(self, hockeyapp_obj, app_id=None,
                 title=None, bundle_id=None, platform=None, release_type=None):
        assert isinstance(hockeyapp_obj, HockeyApp)
        self.hockeyapp_obj = hockeyapp_obj
        self.app_id = app_id
        if self.app_id is not None:
            self.base_url = self.hockeyapp_obj.base_url + '/apps/' + self.app_id
        else:
            self.base_url = None
        self.title = title
        self.bundle_id = bundle_id
        App.check_platform(platform)
        self.platform = platform
        App.check_release_type(release_type)
        self.release_type = release_type

    def __eq__(self, other):
        return (self.app_id == other.app_id and
                self.base_url == other.base_url and
                self.title == other.title and
                self.bundle_id == other.bundle_id and
                self.platform == other.platform and
                self.release_type == other.release_type)

    def desc(self):
        return '<hockeyapp.App: %s %s [%s: %s]>' % (self.title, self.app_id, self.platform, self.bundle_id)

    def __repr__(self):
        return str(self.desc())

    def __str__(self):
        return str(self.desc())

    def versions(self):
        """
        List all versions of this app. Return a list of Version objects.
        """
        response = self.hockeyapp_obj.command(self.base_url + '/app_versions').get().run()
        if response['status'] != 'success':
            raise ValueError('Server returns failures (status=%s)' % response['status'])
        version_list = []
        for version_data in response['app_versions']:
            version = Version(app_obj=self,
                              version_id=version_data['id'],
                              version=version_data['version'],
                              short_version=version_data['shortversion'])
            version_list.append(version)
        return version_list

    def version(self, version_id):
        """
        Return a Version object that represents a single version of this app.
        """
        return Version(app_obj=self,
                       version_id=version_id).read()

    def create(self):
        form_data = dict()
        if self.title is None:
            raise ValueError('title is not initialized')
        else:
            form_data['title'] = self.title
        if self.bundle_id is None:
            raise ValueError('bundle_id is not initialized')
        else:
            form_data['bundle_identifier'] = self.bundle_id
        App.check_platform(self.platform)
        if self.platform is not None:
            form_data['platform'] = self.platform
        App.check_release_type(self.release_type)
        if self.release_type is not None:
            form_data['release_type'] = App.RELEASE_TYPES[self.release_type]

        response = self.hockeyapp_obj.command(self.hockeyapp_obj.base_url + '/apps/new', form_data).post().run()
        if 'errors' in response:
            raise HockeyAppError(response)
        if 'public_identifier' not in response:
            raise HockeyAppError('no public_identifier in response')
        self.app_id = str(response['public_identifier'])
        self.base_url = self.hockeyapp_obj.base_url + '/apps/' + self.app_id
        return self

    def read(self):
        app_list = self.hockeyapp_obj.apps()
        for app in app_list:
            if app.app_id != self.app_id:
                continue
            self.title = app.title
            self.bundle_id = app.bundle_id
            self.platform = app.platform
            self.release_type = app.release_type
            break
        else:
            raise ValueError('Unknown app id %s' % self.app_id)
        return self

    def delete(self):
        self.hockeyapp_obj.command(self.base_url).delete().run()

    def find_version(self, version, short_version):
        for version_obj in self.versions():
            if version_obj.short_version == short_version and version_obj.version == version:
                return version_obj
        return None


class Version:
    def __init__(self, app_obj, version_id=None, version=None, short_version=None):
        assert isinstance(app_obj, App)
        self.app_obj = app_obj
        self.version_id = version_id
        if self.version_id is not None:
            self.base_url = self.app_obj.base_url + '/app_versions/' + str(self.version_id)
        else:
            self.base_url = None
        self.version = version
        self.short_version = short_version

    def __eq__(self, other):
        return (self.version_id == other.version_id and
                self.version == other.version and
                self.short_version == other.short_version and
                self.base_url == other.base_url)

    def desc(self):
        return '<hockeyapp.Version: %s %s %s [%s: %s]>' % (self.short_version, self.version, self.version_id,
                                                           self.app_obj.title, self.app_obj.app_id)

    def __repr__(self):
        return str(self.desc())

    def __str__(self):
        return str(self.desc())

    def update(self, zipped_dsym_file=None, ipa_file=None, note=None):
        form_data = dict()
        if zipped_dsym_file is not None:
            form_data['dsym'] = '@' + zipped_dsym_file
        if ipa_file is not None:
            form_data['ipa'] = '@' + ipa_file
        if note is not None:
            form_data['note'] = note
        if not form_data.keys():
            raise ValueError("form_data can not be empty.")
        response = self.app_obj.hockeyapp_obj.command(self.base_url, form_data).put().run()
        return response

    def create(self):
        form_data = dict()
        if self.version is None:
            raise ValueError('version is not initialized')
        else:
            form_data['bundle_version'] = self.version
        if self.short_version is None:
            raise ValueError('short_version is not initialized')
        else:
            form_data['bundle_short_version'] = self.short_version

        response = self.app_obj.hockeyapp_obj.command(self.app_obj.base_url + '/app_versions/new', form_data).post().run()
        if 'errors' in response:
            raise HockeyAppError(response)
        if 'id' not in response:
            raise HockeyAppError('no id in response')
        self.version_id = response['id']
        self.base_url = self.app_obj.base_url + '/app_versions/' + str(self.version_id)
        return self

    def read(self):
        version_list = self.app_obj.versions()
        for version in version_list:
            if version.version_id == self.version_id:
                self.version = version.version
                self.short_version = version.short_version
                break
        else:
            raise ValueError('Unknown version id %s for app id %s' % (self.version_id, self.app_obj.app_id))
        return self

    def delete(self):
        self.app_obj.hockeyapp_obj.command(self.base_url, {'strategy': 'purge'}).delete().run()
