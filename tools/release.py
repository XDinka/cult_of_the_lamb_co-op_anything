# Publishes a GitHub release with its files (the token comes from git's credential manager).
# GitHub then announces it in Discord (.github/workflows/discord-release.yml, on "published").
# Usage: release.py VERSION "TITLE" NOTES.md INSTALLER.exe PACKAGE.zip MOD.zip [--draft-check]
#   INSTALLER.exe  out/CotlCoopInstaller-VERSION.exe, published as CotlCoopInstaller.exe (the README's button)
#   PACKAGE.zip    out/cotl-coop-anything-VERSION.zip, the manual install
#   MOD.zip        out/CotlCoopMod-VERSION.zip, published as CotlCoopMod.zip: what a running game
#                  downloads to update the mod without leaving
#   --draft-check  everything up to publishing (a draft nobody sees, its files checked), then the draft
#                  is deleted: a rehearsal that announces nothing
# Any other "--" switch, or not exactly six other arguments, stops it before anything is done.
#
# Before anything is made:
# - HEAD is origin/main, the tree is clean, and neither a tag vVERSION on origin nor a release (draft or
#   not) of that tag on GitHub is there;
# - the mod's own version at HEAD (CotlMpVer in COTLMP/Data/Version.cs, what the game compares) is VERSION;
# - the three files give a player one and the same build each way: the installer is one whole PE image
#   (nothing cut, nothing appended), stamped VERSION+HEAD, with one archive in it, among its resources;
#   that payload is the package's game folder file for file, MOD.zip is the package's Mod folder file for
#   file plus a manifest.json that is VERSION and what tools/deploy-mod.sh at HEAD writes; COTLMP.bin
#   carries VERSION+HEAD, COTLMPServer.bin
#   and COTLMP.Loader.dll carry +HEAD; every member of every archive reads (CRC), no name twice in any case.
# Each file is read once: what is checked is what is hashed and what goes up.
#
# The release is made as a draft on the commit that is pushed, its three files go up and each one's
# digest must be the local file's; only then is it published, so /releases/latest never shows a release
# without all of its files. What a failure leaves, as it says:
# - before the draft (checks, GitHub not reached): nothing;
# - making the draft: a draft vVERSION may be there, to delete before running again;
# - files going up or read back: the draft, not public;
# - --draft-check deleting its draft: refused, the draft to delete by hand; no answer, it may still be there;
# - publishing refused (4xx): the draft, not public;
# - publishing with no answer, a 5xx or an answer that is not "published": RELEASE STATE UNKNOWN (not
#   "NOT RELEASED"): it may be public and announced; the releases page tells, before anything else is done.
# A git failure (the network to GitHub, most likely) stops it with git's own words, before anything is made.
import hashlib, http.client, io, json, re, struct, subprocess, sys, urllib.error, urllib.request, zipfile, zlib

# A title or a path the console's code page can't show must not end the run (after publishing, above all)
for stream in (sys.stdout, sys.stderr):
    stream.reconfigure(errors='backslashreplace')


def stop(why):
    sys.exit(f'NOT RELEASED: {why}')


flags = [a for a in sys.argv[1:] if a.startswith('--')]
args = [a for a in sys.argv[1:] if not a.startswith('--')]
if any(f != '--draft-check' for f in flags) or len(args) != 6:
    stop('usage: release.py VERSION "TITLE" NOTES.md INSTALLER.exe PACKAGE.zip MOD.zip [--draft-check] '
         f'(got switches {flags} and {len(args)} arguments)')
check = '--draft-check' in flags
ver, title, notes, exe, zipf, modzip = args
repo = 'XDinka/cult_of_the_lamb_co-op_anything'
api = f'https://api.github.com/repos/{repo}'


def git(*a):
    try:
        return subprocess.run(['git', *a], capture_output=True, text=True, encoding='utf-8', errors='replace',
                              check=True).stdout.strip()
    except subprocess.CalledProcessError as e:
        stop(f'git {" ".join(a)} failed: {(e.stderr or "").strip()[:300]}')
    except OSError as e:
        stop(f'git {" ".join(a)} could not run: {e}')


# The commit the tag goes on: the one that is pushed, from a clean tree, the tag not there yet
head = git('rev-parse', 'HEAD')
remote = (git('ls-remote', 'origin', 'refs/heads/main').split() or [''])[0]
if not remote:
    stop('origin has no main branch (git ls-remote gave nothing)')
if head != remote:
    stop(f'HEAD {head[:12]} is not origin/main {remote[:12]}: push first')
if git('status', '--porcelain', '--untracked-files=no'):
    stop('uncommitted changes in the tree')
if git('ls-remote', 'origin', f'refs/tags/v{ver}'):
    stop(f'the tag v{ver} is on origin already (GitHub would put the release on its commit, not on HEAD)')
# The version the game itself compares (Updater) is CotlMpVer, not the build's: it must be VERSION
game_ver = re.search(r'CotlMpVer = "([^"]*)"', git('show', 'HEAD:COTLMP/Data/Version.cs'))
if not game_ver or game_ver[1] != ver:
    stop(f'COTLMP/Data/Version.cs at HEAD has CotlMpVer {game_ver[1] if game_ver else "(not found)"}, not {ver}')


def carries(data, want):
    return want.encode() in data or want.encode('utf-16-le') in data


MOD = 'BepInEx/plugins/COTLMP/Mod/'


def pe_resources(data):
    """The installer is one whole PE image (nothing cut, nothing appended): its managed resources' span"""
    pe, = struct.unpack_from('<I', data, 0x3C)
    if data[:2] != b'MZ' or data[pe:pe + 4] != b'PE\0\0':
        raise ValueError('not a PE file')
    nsec, = struct.unpack_from('<H', data, pe + 6)
    optsize, = struct.unpack_from('<H', data, pe + 20)
    opt = pe + 24
    dd = opt + (96 if struct.unpack_from('<H', data, opt)[0] == 0x10b else 112)
    secs = [struct.unpack_from('<4I', data, opt + optsize + 40 * i + 8) for i in range(nsec)]
    end = max(rp + rs for _, _, rs, rp in secs)
    cert_at, cert_size = struct.unpack_from('<2I', data, dd + 8 * 4)
    if cert_size:
        end = max(end, cert_at + cert_size)
    if len(data) != end:
        raise ValueError(f'is {len(data)} bytes but its PE image ends at {end}: cut short or something appended')

    def at(rva):
        for vsize, va, rsize, raw in secs:
            if va <= rva < va + max(vsize, rsize):
                return rva - va + raw
        raise ValueError(f'RVA {rva:#x} is in no section')
    clr = at(struct.unpack_from('<I', data, dd + 8 * 14)[0])
    res_rva, res_size = struct.unpack_from('<2I', data, clr + 24)
    return at(res_rva), at(res_rva) + res_size


def strict(raw, eocd, z, where):
    """What .NET's ZipArchive (the installer, the game's update) also demands: one disk, the end record's
    count right, each deflated entry inflating to exactly its size; then every CRC"""
    _, disk, cd_disk, here, total, _, _, _ = struct.unpack_from('<4s4H2LH', raw, eocd)
    if disk or cd_disk or here != total or total != len(z.infolist()) or any(i.volume for i in z.infolist()):
        raise ValueError(f'{where}: a split or miscounted end record')
    for i in z.infolist():
        n, m = struct.unpack_from('<2H', raw, i.header_offset + 26)
        body = raw[i.header_offset + 30 + n + m:i.header_offset + 30 + n + m + i.compress_size]
        if i.compress_type == zipfile.ZIP_DEFLATED:
            d = zlib.decompressobj(-15)
            size = len(d.decompress(body)) + len(d.flush())
            if not d.eof or d.unused_data or size != i.file_size:
                raise ValueError(f'{where}: {i.filename} does not inflate to exactly its size')
        elif i.compress_type != zipfile.ZIP_STORED or len(body) != i.file_size:
            raise ValueError(f'{where}: {i.filename} is stored oddly')


def members(z, where):
    """Every file of an archive by name (read() checks each CRC); a name twice, in any case or with either
    slash, is refused: the installer writes them to NTFS"""
    seen, out = set(), {}
    for i in z.infolist():
        key = i.filename.replace('\\', '/').lower()
        if key in seen:
            raise ValueError(f'{where} has {i.filename} twice')
        seen.add(key)
        if not i.is_dir():
            out[i.filename] = z.read(i)
    return out


def whole_zip(raw, where):
    """A file that is one archive, from its first byte to its last"""
    eocd = raw.rfind(b'PK\x05\x06')
    if eocd < 0 or eocd + 22 > len(raw):
        raise ValueError(f'{where} is not a zip')
    cd_size, cd_at, comment = struct.unpack_from('<2LH', raw, eocd + 12)
    if eocd - cd_size - cd_at != 0 or eocd + 22 + comment != len(raw):
        raise ValueError(f'{where} has bytes before or after its archive')
    z = zipfile.ZipFile(io.BytesIO(raw))
    strict(raw, eocd, z, where)
    return members(z, where)


def end_records(data):
    """Every well-formed archive end record in data: one disk, its counts agreeing and not 0, its central
    directory where it says (PK 1 2 there), all inside data. Four stray bytes PK 5 6 in compressed data,
    code or padding (zeros after them too) are not one of these"""
    found, at = [], data.find(b'PK\x05\x06')
    while at >= 0:
        if at + 22 <= len(data):
            disk, cd_disk, here, total, cd_size, cd_at, comment = struct.unpack_from('<4H2LH', data, at + 4)
            start = at - cd_size - cd_at
            if (disk == cd_disk == 0 and here == total >= 1 and start >= 0 and at + 22 + comment <= len(data)
                    and data[start + cd_at:start + cd_at + 4] == b'PK\x01\x02'):
                found.append((at, start, at + 22 + comment))
        at = data.find(b'PK\x05\x06', at + 1)
    return found


def payload(data, where):
    """The installer's payload: the one archive in it (one well-formed end record in the whole file, as
    built), a managed resource (its length first); returns the installer without it, and its files"""
    lo, hi = pe_resources(data)
    ends = end_records(data)
    if len(ends) != 1:
        raise ValueError(f'{where} has {len(ends)} archive end records, not 1')
    eocd, start, end = ends[0]
    if not lo + 4 <= start < end <= hi or struct.unpack_from('<I', data, start - 4)[0] != end - start:
        raise ValueError(f'{where}: its archive is not one of its resources')
    z = zipfile.ZipFile(io.BytesIO(data[start:end]))
    strict(data[start:end], eocd - start, z, where)
    return data[:start] + data[end:], members(z, where)


def differ(a, b):
    return sorted(n for n in set(a) | set(b) if n not in a or n not in b or a[n] != b[n])


def check_files(ver, head, exe, zipf, modzip, blob, needs):
    """Why the three files (their bytes in blob, by path) are not this build, or None; needs is what
    MOD.zip's manifest.json must say besides the version (tools/deploy-mod.sh at HEAD)"""
    want = f'{ver}+{head}'
    try:
        mod = whole_zip(blob[modzip], modzip)
        manifest = json.loads(mod.pop('manifest.json', b'null'))
        if manifest != {'version': ver, **needs}:
            return f'{modzip}: its manifest.json is {manifest}, not {({"version": ver, **needs})}'
        pkg = whole_zip(blob[zipf], zipf)
        top = f'cotl-coop-anything-{ver}/'
        if any(not n.startswith(top) for n in pkg):
            return f'{zipf}: not all in {top}'
        game = {n[len(top) + 5:]: b for n, b in pkg.items() if n.startswith(top + 'game/')}
        rest, pay = payload(blob[exe], exe)
        if not carries(rest, want):
            return f'{exe} is not stamped {want}'
    except Exception as e:
        return f'a file is not as built: {type(e).__name__}: {e}'
    if differ(pay, game):
        return f'the installer\'s payload and the package\'s game folder differ in {differ(pay, game)}'
    moddir = {n[len(MOD):]: b for n, b in game.items() if n.startswith(MOD)}
    if differ(mod, moddir):
        return f'{modzip} and the package\'s Mod folder differ in {differ(mod, moddir)}'
    built = ((MOD + 'COTLMP.bin', want), (MOD + 'COTLMPServer.bin', '+' + head),
             ('BepInEx/plugins/COTLMP/COTLMP.Loader.dll', '+' + head))
    for path, stamp in built:
        if path not in game or not carries(game[path], stamp):
            return f'{path} is not built from {head} (no {stamp})'
    for path, _ in built:
        print(f'{path}: sha256 {hashlib.sha256(game[path]).hexdigest()}')
    print(f'{len(pay)} payload files = the package\'s game folder, {len(mod)} files of {modzip} = its Mod folder')
    return None


blob = {}
for path in (exe, zipf, modzip):
    try:
        with open(path, 'rb') as f:
            blob[path] = f.read()
    except OSError as e:
        stop(f'cannot read {path}: {e}')
try:
    with open(notes, encoding='utf-8-sig') as f:
        body = f.read()
except (OSError, ValueError) as e:
    stop(f'cannot read the notes {notes}: {e}')
# What the update's manifest names besides the version, as tools/deploy-mod.sh at HEAD writes it
deploy = git('show', 'HEAD:tools/deploy-mod.sh')


def assigned(name):
    """NAME's value in deploy-mod.sh, read as bash reads it (export/readonly, quotes, a # comment after);
    it must be set once"""
    lines = [l for l in deploy.splitlines() if re.match(rf'\s*(?:(?:export|readonly)\s+)?{name}=', l)]
    one = re.fullmatch(rf'\s*(?:(?:export|readonly)\s+)?{name}=(?:"([^"]*)"|\'([^\']*)\'|([^\s"\'#]*))\s*(?:#.*)?',
                       lines[0]) if len(lines) == 1 else None
    if not one:
        stop(f'tools/deploy-mod.sh at HEAD sets {name} {len(lines)} times, or not plainly: {lines}')
    return next(v for v in one.groups() if v is not None)


contract = re.findall(r'\bContract\s*=\s*(\d+)\s*;', git('show', 'HEAD:COTLMP/Network/HotUpdate.cs'))
if len(contract) != 1:
    stop(f'COTLMP/Network/HotUpdate.cs at HEAD has {len(contract)} loader contracts, not 1')
needs = {'loader': int(contract[0]), 'bepinex': assigned('BEPINEX'), 'cotlapi': assigned('COTLAPI')}
why = check_files(ver, head, exe, zipf, modzip, blob, needs)
if why:
    stop(why)
print('files carry', f'{ver}+{head}')

try:
    cred = subprocess.run(['git', 'credential', 'fill'], input='protocol=https\nhost=github.com\n\n', capture_output=True, text=True).stdout
except OSError as e:
    stop(f'git credential fill could not run: {e}')
tok = dict(l.split('=', 1) for l in cred.splitlines() if '=' in l).get('password')
if not tok:
    stop('git credential fill gave no GitHub token: sign in to GitHub in git first')
H = {'Authorization': 'token ' + tok, 'Accept': 'application/vnd.github+json'}


def call(url, data=None, method='GET', headers=None, timeout=60, expect=dict, keys=()):
    """One GitHub call: an HTTP error stays an HTTPError, any other failure (no answer in time, the
    connection lost, a broken answer, one without what the step reads from it) becomes a URLError, so
    each step below says what it left. expect=None takes an empty answer"""
    req = urllib.request.Request(url, data=data, headers={**H, **(headers or {})}, method=method)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            text = r.read()
        got = json.loads(text) if text else None
    except urllib.error.URLError:
        raise
    except (OSError, http.client.HTTPException, ValueError) as e:
        raise urllib.error.URLError(f'{type(e).__name__}: {e}') from e
    if expect is not None and (not isinstance(got, expect) or any(k not in got for k in keys)):
        raise urllib.error.URLError(f'an answer without what was asked: {str(got)[:120]}')
    return got


def said(e):
    """An error as told: an HTTP one with what GitHub said (a 422's reason is in its body)"""
    if isinstance(e, urllib.error.HTTPError):
        try:
            return f'{e}: {e.read()[:300].decode("utf-8", "replace")}'
        except Exception:
            return str(e)
    return str(e)


def unknown(why):
    sys.exit(f'RELEASE STATE UNKNOWN (it may be public and announced): {why}; check the releases page before anything')


# Neither a published release of the tag nor any draft of it (/releases/tags/ never shows drafts)
try:
    try:
        call(f'{api}/releases/tags/v{ver}')
        stop(f'release v{ver} is there already')
    except urllib.error.HTTPError as e:
        if e.code != 404:
            raise
    for page in range(1, 11):
        listed = call(f'{api}/releases?per_page=100&page={page}', expect=list)
        same = [r.get('id') for r in listed if isinstance(r, dict) and r.get('tag_name') == f'v{ver}']
        if same:
            stop(f'GitHub has a release (or draft) of v{ver} already: {same}; delete it first')
        if len(listed) < 100:
            break
    else:
        stop('GitHub lists 1000 releases or more: a draft of this tag past them would not be seen; nothing was made')
except urllib.error.URLError as e:
    stop(f'cannot reach GitHub ({said(e)}): nothing was made')

files = ((exe, 'CotlCoopInstaller.exe', 'application/octet-stream'),
         (zipf, f'cotl-coop-anything-{ver}.zip', 'application/zip'),
         (modzip, 'CotlCoopMod.zip', 'application/zip'))
digests = {name: 'sha256:' + hashlib.sha256(blob[path]).hexdigest() for path, name, _ in files}

try:
    rel = call(f'{api}/releases', json.dumps({'tag_name': 'v' + ver, 'target_commitish': head, 'name': f'v{ver}: {title}',
                                              'body': body, 'prerelease': False, 'draft': True}).encode(), 'POST',
               keys=('id', 'upload_url'))
except urllib.error.URLError as e:
    stop(f'making the draft failed ({said(e)}): a draft v{ver} may be on GitHub, delete it before running again')
if rel.get('draft') is not True:
    unknown(f'the release {rel["id"]} was made, but not as a draft (draft: {rel.get("draft")})')
print('draft', rel['id'], 'on', head[:12])
try:
    up = rel['upload_url'].split('{')[0]
    for path, name, ct in files:
        # One time limit covers the whole body going up: a minute, and a second more per 2 KB
        a = call(f'{up}?name={name}', blob[path], 'POST', {'Content-Type': ct}, timeout=60 + len(blob[path]) // 2048,
                 keys=('digest',))
        if a.get('digest') != digests[name]:
            stop(f'{name} went up as {a.get("digest")}, the file is {digests[name]} (the draft {rel["id"]} is left, not public)')
        print('up', name, a['digest'])
    assets = call(f'{api}/releases/{rel["id"]}', keys=('assets',))['assets']
    got = {a.get('name'): a.get('digest') for a in assets if isinstance(a, dict)}
    if got != digests:
        stop(f'the draft has {got}, expected {digests} (the draft {rel["id"]} is left, not public)')
except urllib.error.URLError as e:
    stop(f'{said(e)} (the draft {rel["id"]} is left, not public)')

if check:
    try:
        call(f'{api}/releases/{rel["id"]}', method='DELETE', expect=None)
    except urllib.error.HTTPError as e:
        stop(f'the rehearsal passed, but deleting the draft {rel["id"]} was refused ({said(e)}): delete it by hand')
    except urllib.error.URLError as e:
        stop(f'the rehearsal passed, but deleting the draft {rel["id"]} got no answer ({e}): '
             f'the draft {rel["id"]} may still be there: check and delete it')
    print('draft check passed: the draft and its files are deleted, nothing published')
    sys.exit(0)

try:
    rel = call(f'{api}/releases/{rel["id"]}', json.dumps({'draft': False}).encode(), 'PATCH', keys=('html_url',))
except urllib.error.HTTPError as e:
    if 400 <= e.code < 500:
        stop(f'publishing was refused ({said(e)}): the draft {rel["id"]} is left, not public')
    unknown(f'publishing was answered {said(e)}')
except urllib.error.URLError as e:
    unknown(f'publishing got no answer ({e})')
if rel.get('draft') is not False:
    unknown(f'GitHub answered, but not that the release is published (draft: {rel.get("draft")})')
for a in rel.get('assets') or []:
    print(a.get('browser_download_url'), a.get('digest'))
print(rel['html_url'], rel.get('name'))
