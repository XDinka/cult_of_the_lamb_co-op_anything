"""
Announces a GitHub release in the Discord releases channel through its webhook.

    python tools/discord-release.py TAG            (posts)
    python tools/discord-release.py TAG --dry-run  (prints the message instead)

The webhook comes from DISCORD_WEBHOOK or from the file .discord-webhook at the repository root
(git-ignored, never commit it). Run by tools/release.py after it publishes a release, or by
.github/workflows/discord-release.yml when the repository has the DISCORD_WEBHOOK secret (use one of
the two, not both, or the release is announced twice). The words for players can
sit in the release notes as hidden blocks, which GitHub doesn't show:

    <!-- discord-ru
    **Арабский читается правильно.** ...
    -->
    <!-- discord-en
    **Arabic reads right.** ...
    -->

Without them the message lists the bold headings of the notes (not "Tested:" and the like). The first picture
of the notes goes under the message.
"""
import json
import os
import re
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = "XDinka/cult_of_the_lamb_co-op_anything"
SITE = f"https://github.com/{REPO}"
AVATAR = f"https://raw.githubusercontent.com/{REPO}/main/Media/readme/lamb.png"
RED = 0xC41E2A

LINKS = {
    "ru": ("🇷🇺", "Скачать установщик", "Вики", "Все изменения", f"{SITE}/wiki/Главная",
           "Обновите мод у обоих игроков: версии должны совпадать."),
    "en": ("🇬🇧", "Download the installer", "Wiki", "Full changelog", f"{SITE}/wiki",
           "Both players need to update: versions must match."),
}


def release(tag):
    headers = {"Accept": "application/vnd.github+json", "User-Agent": "discord-release"}
    if os.environ.get("GITHUB_TOKEN"):
        headers["Authorization"] = "Bearer " + os.environ["GITHUB_TOKEN"]
    req = urllib.request.Request(f"https://api.github.com/repos/{REPO}/releases/tags/{tag}", headers=headers)
    return json.load(urllib.request.urlopen(req))


def headings(body):
    found = re.findall(r"^\*\*(.+?)\*\*", body, re.M)
    # "Tested:", "Install:" and the like open service parts, not news
    return "\n".join("• " + h for h in found if not h.rstrip().endswith(":"))[:3000]


def embed(lang, text, rel, version):
    flag, download, wiki, changes, wiki_url, note = LINKS[lang]
    links = (f"⬇️ **[{download}]({SITE}/releases/latest/download/CotlCoopInstaller.exe)**  ·  "
             f"📖 [{wiki}]({wiki_url})  ·  📝 [{changes}]({rel['html_url']})")
    # Each embed gets its own link: Discord folds embeds with the same link into one, keeping only
    # the first one's text
    return {"title": f"{flag} Co-op Anything {version}", "url": f"{rel['html_url']}#{lang}", "color": RED,
            "description": f"{text.strip()}\n\n{links}\n*{note}*"}


def message(rel):
    body = rel.get("body") or ""
    version = rel["tag_name"].lstrip("v")
    blocks = dict(re.findall(r"<!--\s*discord-(ru|en)\s*\n(.*?)-->", body, re.S))
    if blocks:
        embeds = [embed(lang, blocks[lang], rel, version) for lang in ("ru", "en") if lang in blocks]
    else:
        embeds = [embed("en", headings(body), rel, version)]
    picture = re.search(r"!\[[^\]]*\]\((https://[^)\s]+)\)", body)
    if picture:
        embeds[-1]["image"] = {"url": picture.group(1)}
    return {"username": "Co-op Anything", "avatar_url": AVATAR, "embeds": embeds}


def webhook():
    if os.environ.get("DISCORD_WEBHOOK"):
        return os.environ["DISCORD_WEBHOOK"]
    path = os.path.join(ROOT, ".discord-webhook")
    return open(path, encoding="utf-8").read().strip() if os.path.exists(path) else None


def main():
    tag = sys.argv[1]
    rel = release(tag)
    if rel.get("draft") or rel.get("prerelease"):
        print(f"{tag} is a draft or a prerelease, not announced")
        return
    data = message(rel)
    if "--dry-run" in sys.argv:
        print(json.dumps(data, ensure_ascii=False, indent=2))
        return
    url = webhook()
    if not url:
        print("No DISCORD_WEBHOOK and no .discord-webhook file, not announced")
        return
    req = urllib.request.Request(url + "?wait=true",
                                 data=json.dumps(data).encode(), method="POST",
                                 headers={"Content-Type": "application/json", "User-Agent": "discord-release"})
    print("Posted", json.load(urllib.request.urlopen(req))["id"])


if __name__ == "__main__":
    main()
