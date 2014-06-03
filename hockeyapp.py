import subprocess
import json


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
        return json.loads(output)


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
            app_list.append(app)
        return app_list

    def app(self, app_id):
        return App(hockeyapp_obj=self, app_id=app_id)

    def delete_app(self, app_id):
        app = App(hockeyapp_obj=self, app_id=app_id)
        app.delete()


class App:
    def __init__(self, hockeyapp_obj, app_id,
                 title=None, bundle_id=None, platform=None):
        assert isinstance(hockeyapp_obj, HockeyApp)
        self.hockeyapp_obj = hockeyapp_obj
        self.app_id = app_id
        self.base_url = self.hockeyapp_obj.base_url + '/apps/' + app_id
        self.title = title
        self.bundle_id = bundle_id
        self.platform = platform

        if self.title is None or self.bundle_id is None or self.platform is None:
            app_list = self.hockeyapp_obj.apps()
            for app in app_list:
                if app.app_id == self.app_id:
                    self.title = app.title
                    self.bundle_id = app.bundle_id
                    self.platform = app.platform
                    break
            else:
                raise ValueError('Unknown app id %s' % self.app_id)

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
                       version_id=version_id)

    def delete(self):
        self.hockeyapp_obj.command(self.base_url).delete().run()


class Version:
    def __init__(self, app_obj, version_id, version=None, short_version=None):
        assert isinstance(app_obj, App)
        self.app_obj = app_obj
        self.version_id = version_id
        self.base_url = self.app_obj.base_url + '/app_versions/' + str(self.version_id)
        self.version = version
        self.short_version = short_version

        if self.version is None or self.short_version is None:
            version_list = self.app_obj.versions()
            for version in version_list:
                if version.version_id == self.version_id:
                    self.version = version.version
                    self.short_version = version.short_version
                    break
            else:
                raise ValueError('Unknown version id %s for app id %s' % (self.version_id, self.app_obj.app_id))

    def desc(self):
        return '<hockeyapp.Version: %s %s %s [%s: %s]>' % (self.short_version, self.version, self.version_id,
                                                           self.app_obj.title, self.app_obj.app_id)

    def __repr__(self):
        return str(self.desc())

    def __str__(self):
        return str(self.desc())

    def update(self, zipped_dsym_file, note=None):
        form_data = dict()
        form_data['dsym'] = '@' + zipped_dsym_file
        if note is not None:
            form_data['note'] = note
        response = self.app_obj.hockeyapp_obj.command(self.base_url, form_data).put().run()
        return response


if __name__ == '__main__':
    ha = HockeyApp(api_token='92c7e2b0e98642f3b6ad1e3f6403924c')
    apps = ha.apps()
    print apps
    versions = apps[0].versions()
    print versions
    versions[0].update('./NachoClientiOS.dSYM.zip')