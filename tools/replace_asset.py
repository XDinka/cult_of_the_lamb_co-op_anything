# Swaps one file of a published GitHub release (the token comes from git's credential manager).
# Usage: replace_asset.py VERSION FILE ASSET_NAME   (e.g. 0.3.6 out/CotlCoopMod-0.3.6.zip CotlCoopMod.zip)
import json, subprocess, urllib.request, sys
ver, path, name = sys.argv[1:4]
cred = subprocess.run(['git', 'credential', 'fill'], input='protocol=https\nhost=github.com\n\n', capture_output=True, text=True).stdout
tok = dict(l.split('=', 1) for l in cred.splitlines() if '=' in l)['password']
H = {'Authorization': 'token ' + tok, 'Accept': 'application/vnd.github+json'}
repo = 'XDinka/cult_of_the_lamb_co-op_anything'
rel = json.load(urllib.request.urlopen(urllib.request.Request(f'https://api.github.com/repos/{repo}/releases/tags/v{ver}', headers=H)))
for a in rel['assets']:
    if a['name'] == name:
        urllib.request.urlopen(urllib.request.Request(a['url'], headers=H, method='DELETE'))
        print('removed old', a['name'], a.get('digest'))
up = rel['upload_url'].split('{')[0]
r = urllib.request.Request(f'{up}?name={name}', data=open(path, 'rb').read(), headers={**H, 'Content-Type': 'application/octet-stream'}, method='POST')
a = json.load(urllib.request.urlopen(r))
print(a['browser_download_url'], a.get('digest'))
