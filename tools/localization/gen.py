"""
Writes COTLMP/Language/<code>.cs from the dictionaries here (one file per language, term -> text), in the
order of the English table. A term missing from a language stops it: every string goes into all 15 tables
(English and Russian are written by hand).

    python tools/localization/gen.py
"""
import importlib.util
import json
import re
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
_EN = open(os.path.join(REPO, "COTLMP", "Language", "en-US.cs"), encoding="utf-8-sig").read()
EN = [{"term": t, "over": o} for t, _, o in re.findall(r'new\("([^"]+)",\s*(\$?"(?:[^"\\]|\\.)*"),\s*(true|false)\)', _EN)]

LANGS = [
    # module, file, class, language name, english name in the header
    ("ja", "ja-JP", "Japanese", "Japanese", "Japanese"),
    ("fr", "fr-FR", "French", "French", "French"),
    ("de", "de-DE", "German", "German", "German"),
    ("es", "es-ES", "Spanish", "Spanish", "Spanish"),
    ("pt", "pt-BR", "PortugueseBrazil", "Portuguese (Brazil)", "Portuguese (Brazil)"),
    ("zhs", "zh-Hans", "ChineseSimplified", "Chinese (Simplified)", "Chinese (Simplified)"),
    ("zht", "zh-Hant", "ChineseTraditional", "Chinese (Traditional)", "Chinese (Traditional)"),
    ("ko", "ko-KR", "Korean", "Korean", "Korean"),
    ("it", "it-IT", "Italian", "Italian", "Italian"),
    ("nl", "nl-NL", "Dutch", "Dutch", "Dutch"),
    ("tr", "tr-TR", "Turkish", "Turkish", "Turkish"),
    ("frca", "fr-CA", "FrenchCanadian", "French (Canadian)", "French (Canadian)"),
    ("ar", "ar", "Arabic", "Arabic", "Arabic"),
]


def load(name):
    # Canadian French: the French table (none of its lines reads differently in Quebec)
    if name == "frca":
        return dict(load("fr"))
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod.T


def literal(text):
    if "{VER}" in text:
        return '$"' + text.replace('"', '\\"').replace("{VER}", "{COTLMP.Data.Version.CotlMpVer}") + '"'
    return '"' + text.replace('"', '\\"') + '"'


for mod, code, cls, lang, english in LANGS:
    T = load(mod)
    missing = [e["term"] for e in EN if e["term"] not in T]
    extra = [t for t in T if t not in {e["term"] for e in EN}]
    if missing or extra:
        raise SystemExit(f"{mod}: missing {missing} extra {extra}")
    lines = []
    for e in EN:
        lines.append(f'            new("{e["term"]}", {literal(T[e["term"]])}, {e["over"]}),')
    lines[-1] = lines[-1].rstrip(",")
    body = "\n".join(lines)
    text = f"""/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     {english} localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{{
    public class {cls}
    {{
        public static LocalizationTable[] StringsTable =
        [
{body}
        ];
    }}
}}

/* EOF */
"""
    out = os.path.join(REPO, "COTLMP", "Language", code + ".cs")
    open(out, "wb").write(b"\xef\xbb\xbf" + text.replace("\n", "\r\n").encode("utf-8"))
    print(out, len(EN))
