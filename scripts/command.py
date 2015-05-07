import threading
import subprocess


class Command(threading.Thread):
    """
    A command object takes in a shell command, executes it in its own thread and returns the status code,
    stdout and stderr output.
    """
    def __init__(self, cmd, cwd=None):
        super(Command, self).__init__()
        self.cmd = cmd
        self.cwd = cwd
        self.proc = None
        # The return code of the command
        self.return_code = None
        # Output of stdout
        self.stdout = None
        # Output of stderr
        self.stderr = None

    def execute(self):
        self.start()
        return self.wait()

    def start(self):
        # Create two string files for captureing stdout and stderr
        self.proc = subprocess.Popen(self.cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, cwd=self.cwd)
        # TODO - need to catch exeception to make sure thread terminate nicely
        (self.stdout, self.stderr) = self.proc.communicate()

    def wait(self):
        self.return_code = self.proc.wait()
        return self.return_code

    def ran_ok(self):
        return self.return_code == 0
