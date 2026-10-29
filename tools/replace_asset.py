# Swaps one file of a published GitHub release (the token comes from git's credential manager).
# Usage: replace_asset.py VERSION FILE ASSET_NAME   (e.g. 0.3.6 out/CotlCoopMod-0.3.6.zip CotlCoopMod.zip)
#
# The new file goes up under a temporary name first and its digest must be the local file's; only then is
# the old one deleted and the new one renamed, so the release is never left without the file. A failure
# before the swap leaves the old file as it was (and the temporary one, to delete by hand).
import hashlib, json, subprocess, sys, urllib.request

ver, path, name = sys.argv[1:4]
cred = subprocess.run(['git', 'credential', 'fill'], input='protocol=https\nhost=github.com\n\n', capture_output=True, text=True).stdout
tok = dict(l.split('=', 1) for l in cred.splitlines() if '=' in l)['password']
H = {'Authorization': 'token ' + tok, 'Accept': 'application/vnd.github+json'}
repo = 'XDinka/cult_of_the_lamb_co-op_anything'


def call(url, data=None, method='GET', headers=None):
    req = urllib.request.Request(url, data=data, headers={**H, **(headers or {})}, method=method)
    with urllib.request.urlopen(req) as r:
        text = r.read()
    return json.loads(text) if text else None


want = 'sha256:' + hashlib.sha256(open(path, 'rb').read()).hexdigest()
rel = call(f'https://api.github.com/repos/{repo}/releases/tags/v{ver}')
up = rel['upload_url'].split('{')[0]
temp = 'next-' + name
new = call(f'{up}?name={temp}', open(path, 'rb').read(), 'POST', {'Content-Type': 'application/octet-stream'})
if new.get('digest') != want:
    sys.exit(f'NOT SWAPPED: {temp} went up as {new.get("digest")}, the file is {want}; the old {name} is as it was')
print('up', temp, new['digest'])
for a in rel['assets']:
    if a['name'] == name:
        call(a['url'], method='DELETE')
        print('removed old', a['name'], a.get('digest'))
a = call(new['url'], json.dumps({'name': name}).encode(), 'PATCH')
print(a['browser_download_url'], a.get('digest'))
