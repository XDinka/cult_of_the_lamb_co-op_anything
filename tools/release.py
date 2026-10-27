# Publishes a GitHub release with its files (the token comes from git's credential manager).
# GitHub then announces it in Discord (.github/workflows/discord-release.yml).
# Usage: release.py VERSION "TITLE" NOTES.md INSTALLER.exe PACKAGE.zip MOD.zip
#   INSTALLER.exe  out/CotlCoopInstaller-VERSION.exe, published as CotlCoopInstaller.exe (the README's button)
#   PACKAGE.zip    out/cotl-coop-anything-VERSION.zip, the manual install
#   MOD.zip        out/CotlCoopMod-VERSION.zip, published as CotlCoopMod.zip: what a running game
#                  downloads to update the mod without leaving
import json, subprocess, sys, urllib.request

ver, title, notes, exe, zipf, modzip = sys.argv[1:7]
cred = subprocess.run(['git', 'credential', 'fill'], input='protocol=https\nhost=github.com\n\n', capture_output=True, text=True).stdout
tok = dict(l.split('=', 1) for l in cred.splitlines() if '=' in l)['password']
H = {'Authorization': 'token ' + tok, 'Accept': 'application/vnd.github+json'}
repo = 'XDinka/cult_of_the_lamb_co-op_anything'
body = open(notes, encoding='utf-8').read()
req = urllib.request.Request(f'https://api.github.com/repos/{repo}/releases',
                             data=json.dumps({'tag_name': 'v' + ver, 'target_commitish': 'main', 'name': f'v{ver}: {title}',
                                              'body': body, 'prerelease': False}).encode(), headers=H, method='POST')
rel = json.load(urllib.request.urlopen(req))
up = rel['upload_url'].split('{')[0]
for path, name, ct in ((exe, 'CotlCoopInstaller.exe', 'application/octet-stream'),
                       (zipf, f'cotl-coop-anything-{ver}.zip', 'application/zip'),
                       (modzip, 'CotlCoopMod.zip', 'application/zip')):
    r = urllib.request.Request(f'{up}?name={name}', data=open(path, 'rb').read(), headers={**H, 'Content-Type': ct}, method='POST')
    print(json.load(urllib.request.urlopen(r))['browser_download_url'])
print(rel['html_url'], rel['name'])
