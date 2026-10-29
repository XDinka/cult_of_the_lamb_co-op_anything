# Publishes a GitHub release with its files (the token comes from git's credential manager).
# GitHub then announces it in Discord (.github/workflows/discord-release.yml, on "published").
# Usage: release.py VERSION "TITLE" NOTES.md INSTALLER.exe PACKAGE.zip MOD.zip [--draft-check]
#   INSTALLER.exe  out/CotlCoopInstaller-VERSION.exe, published as CotlCoopInstaller.exe (the README's button)
#   PACKAGE.zip    out/cotl-coop-anything-VERSION.zip, the manual install
#   MOD.zip        out/CotlCoopMod-VERSION.zip, published as CotlCoopMod.zip: what a running game
#                  downloads to update the mod without leaving
#   --draft-check  everything up to publishing (a draft nobody sees, its files checked), then the draft
#                  is deleted: a rehearsal that announces nothing
#
# The release is made as a draft on the commit that is pushed (HEAD = origin/main, a clean tree), its
# three files go up and each one's digest must be the local file's; only then is it published, so
# /releases/latest never shows a release without all of its files. A failure leaves the draft (not
# public) and says so.
import hashlib, json, subprocess, sys, urllib.error, urllib.request

args = [a for a in sys.argv[1:] if not a.startswith('--')]
check = '--draft-check' in sys.argv
ver, title, notes, exe, zipf, modzip = args[:6]
repo = 'XDinka/cult_of_the_lamb_co-op_anything'
api = f'https://api.github.com/repos/{repo}'


def git(*a):
    return subprocess.run(['git', *a], capture_output=True, text=True, check=True).stdout.strip()


def stop(why):
    sys.exit(f'NOT RELEASED: {why}')


# The commit the tag goes on: the one that is pushed, from a clean tree
head = git('rev-parse', 'HEAD')
remote = git('ls-remote', 'origin', 'refs/heads/main').split()[0]
if head != remote:
    stop(f'HEAD {head[:12]} is not origin/main {remote[:12]}: push first')
if git('status', '--porcelain', '--untracked-files=no'):
    stop('uncommitted changes in the tree')

cred = subprocess.run(['git', 'credential', 'fill'], input='protocol=https\nhost=github.com\n\n', capture_output=True, text=True).stdout
tok = dict(l.split('=', 1) for l in cred.splitlines() if '=' in l)['password']
H = {'Authorization': 'token ' + tok, 'Accept': 'application/vnd.github+json'}


def call(url, data=None, method='GET', headers=None):
    req = urllib.request.Request(url, data=data, headers={**H, **(headers or {})}, method=method)
    with urllib.request.urlopen(req) as r:
        text = r.read()
    return json.loads(text) if text else None


try:
    call(f'{api}/releases/tags/v{ver}')
    stop(f'release v{ver} is there already')
except urllib.error.HTTPError as e:
    if e.code != 404:
        raise

files = ((exe, 'CotlCoopInstaller.exe', 'application/octet-stream'),
         (zipf, f'cotl-coop-anything-{ver}.zip', 'application/zip'),
         (modzip, 'CotlCoopMod.zip', 'application/zip'))
digests = {name: 'sha256:' + hashlib.sha256(open(path, 'rb').read()).hexdigest() for path, name, _ in files}

body = open(notes, encoding='utf-8').read()
rel = call(f'{api}/releases', json.dumps({'tag_name': 'v' + ver, 'target_commitish': head, 'name': f'v{ver}: {title}',
                                          'body': body, 'prerelease': False, 'draft': True}).encode(), 'POST')
print('draft', rel['id'], 'on', head[:12])
try:
    up = rel['upload_url'].split('{')[0]
    for path, name, ct in files:
        a = call(f'{up}?name={name}', open(path, 'rb').read(), 'POST', {'Content-Type': ct})
        if a.get('digest') != digests[name]:
            stop(f'{name} went up as {a.get("digest")}, the file is {digests[name]} (the draft {rel["id"]} is left, not public)')
        print('up', name, a['digest'])
    got = {a['name']: a.get('digest') for a in call(f'{api}/releases/{rel["id"]}')['assets']}
    if got != digests:
        stop(f'the draft has {got}, expected {digests} (the draft {rel["id"]} is left, not public)')
except urllib.error.URLError as e:
    stop(f'{e} (the draft {rel["id"]} is left, not public)')

if check:
    call(f'{api}/releases/{rel["id"]}', method='DELETE')
    print('draft check passed: the draft and its files are deleted, nothing published')
    sys.exit(0)

rel = call(f'{api}/releases/{rel["id"]}', json.dumps({'draft': False}).encode(), 'PATCH')
for a in rel['assets']:
    print(a['browser_download_url'], a.get('digest'))
print(rel['html_url'], rel['name'])
