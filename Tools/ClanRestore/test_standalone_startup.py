"""Start the real single-file Functions Framework server and read only its health.

Pass a prepared directory with index.js and the unchanged dependencies installed.
No signed player request, cloud database operation or deployment is performed.
"""
import argparse
import json
from pathlib import Path
import shutil
import socket
import subprocess
import time
import urllib.request

def check(directory,revision='2026-10-24.clans.node.1'):
    directory=Path(directory).resolve()
    with socket.socket() as available:
        available.bind(('127.0.0.1',0));port=available.getsockname()[1]
    command=[shutil.which('node'),str(directory/'node_modules/@google-cloud/functions-framework/build/src/main.js'),
             '--target=helloHttp','--port='+str(port)]
    process=subprocess.Popen(command,cwd=directory,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    try:
        for _ in range(100):
            try:
                with urllib.request.urlopen('http://127.0.0.1:'+str(port)+'/',timeout=1) as response:
                    body=json.load(response)
                    assert response.status==200
                    assert body=={'ok':True,'revision':revision,'service':'dimensions-front-purchase-server'}
                    print('Actual Node dependencies and Functions Framework startup/health passed; no cloud data accessed.')
                    return
            except OSError:
                if process.poll() is not None:raise RuntimeError(process.stdout.read())
                time.sleep(.1)
        raise RuntimeError('Local Functions Framework startup timed out')
    finally:
        process.terminate()
        try:process.wait(timeout=5)
        except subprocess.TimeoutExpired:process.kill();process.wait()

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('directory',type=Path)
    parser.add_argument('--revision',default='2026-10-24.clans.node.1')
    arguments=parser.parse_args();check(arguments.directory,arguments.revision)
