"""Generuje src/ArcSperanzaIntel.Core/Localization/Texts.<Lang>.cs z plików tools/i18n/<code>.json.
Sprawdza: komplet kluczy względem Texts.cs, te same placeholdery {0}, {1}…, spacje na początku/końcu."""
import json, re, sys, pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
TEXTS = ROOT / "src/ArcSperanzaIntel.Core/Localization/Texts.cs"
LANGS = {"de": "TextsDe", "fr": "TextsFr", "es": "TextsEs", "pt-BR": "TextsPtBr", "ru": "TextsRu", "ja": "TextsJa", "zh-CN": "TextsZh"}

pat = re.compile(r'\["([^"]+)"\]\s*=\s*\((".*?(?<!\\)"),\s*(".*?(?<!\\)")\),\s*$', re.M)
master = {m.group(1): json.loads(m.group(3)) for m in pat.finditer(TEXTS.read_text(encoding="utf-8"))}
ph = re.compile(r"\{\d+(?::[^}]*)?\}")

def cs(s): return json.dumps(s, ensure_ascii=False)

ok = True
for code, cls in LANGS.items():
    src = ROOT / f"tools/i18n/{code}.json"
    if not src.exists():
        print(f"{code}: brak pliku"); ok = False; continue
    tr = json.loads(src.read_text(encoding="utf-8"))
    missing = [k for k in master if k not in tr]
    extra = [k for k in tr if k not in master]
    bad = []
    for k, en in master.items():
        t = tr.get(k)
        if t is None: continue
        if sorted(ph.findall(en)) != sorted(ph.findall(t)): bad.append(f"{k}: placeholder")
        if (en[:1] == " ") != (t[:1] == " ") or (en[-1:] == " ") != (t[-1:] == " "): bad.append(f"{k}: spacje")
    for msg, lst in (("brak", missing), ("nadmiar", extra), ("błąd", bad)):
        if lst: ok = False; print(f"{code} {msg}: {lst}")
    lines = [f'        [{cs(k)}] = {cs(tr[k])},' for k in master if k in tr]
    out = ROOT / f"src/ArcSperanzaIntel.Core/Localization/{cls}.cs"
    out.write_text(
        "namespace ArcSperanzaIntel.Core.Localization;\n\n"
        f"/// <summary>Teksty interfejsu: {code}. Generowane przez tools/i18n_gen.py z tools/i18n/{code}.json — edytuj JSON.</summary>\n"
        f"public static class {cls}\n{{\n"
        "    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>\n    {\n"
        + "\n".join(lines) + "\n    };\n}\n", encoding="utf-8")
    print(f"{code}: {len(lines)}/{len(master)}")
sys.exit(0 if ok else 1)
