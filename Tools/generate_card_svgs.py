from pathlib import Path
import textwrap
import html


OUT_DIR = Path("Assets/cards/generated")
OBJECTIVE_DIR = OUT_DIR / "objective_it"
RULE_DIR = OUT_DIR / "rule_it"


OBJECTIVES = [
    ("obj-24", "Conquista 24 Territori", "Conquista 24 territori.", ""),
    ("obj-18-2", "Conquista 18 con 2 Armate", "Conquista 18 territori e occupali con almeno 2 armate ciascuno.", ""),
    ("obj-eu-au-plus1", "Europa + Oceania + 1", "Conquista Europa, Oceania e un altro continente a scelta.", ""),
    ("obj-eu-sa-plus1", "Europa + Sud America + 1", "Conquista Europa, Sud America e un altro continente a scelta.", ""),
    ("obj-na-af", "Nord America + Africa", "Conquista Nord America e Africa.", ""),
    ("obj-na-au", "Nord America + Oceania", "Conquista Nord America e Oceania.", ""),
    ("obj-as-sa", "Asia + Sud America", "Conquista Asia e Sud America.", ""),
    ("obj-as-af", "Asia + Africa", "Conquista Asia e Africa.", ""),
    ("obj-elim-red", "Distruggi Rosso", "Distruggi totalmente l'armata rossa.", "Se impossibile, conquista 24 territori."),
    ("obj-elim-blue", "Distruggi Blu", "Distruggi totalmente l'armata blu.", "Se impossibile, conquista 24 territori."),
    ("obj-elim-green", "Distruggi Verde", "Distruggi totalmente l'armata verde.", "Se impossibile, conquista 24 territori."),
    ("obj-elim-yellow", "Distruggi Giallo", "Distruggi totalmente l'armata gialla.", "Se impossibile, conquista 24 territori."),
    ("obj-elim-purple", "Distruggi Viola", "Distruggi totalmente l'armata viola.", "Se impossibile, conquista 24 territori."),
    ("obj-elim-black", "Distruggi Nero", "Distruggi totalmente l'armata nera.", "Se impossibile, conquista 24 territori."),
    ("obj-fallback-24", "Conquista 24 Territori", "Conquista 24 territori.", ""),
]


RULE_CARDS = [
    (
        "rule_card.turn_sequence",
        "Sequenza Turno",
        [
            "1. Rinforza",
            "2. Attacca (opzionale, ripetibile)",
            "3. Fortifica (opzionale, una volta)",
            "4. Fine turno e pesca carta se hai conquistato",
        ],
    ),
    (
        "rule_card.combat_resolution",
        "Risoluzione Combattimento",
        [
            "Attaccante: 1-3 dadi",
            "Difensore: fino al massimo profilo",
            "Confronta dadi in ordine decrescente",
            "Pareggio: vince il difensore",
        ],
    ),
    (
        "rule_card.trade_in",
        "Valori Tris",
        [
            "3 artiglierie = 4",
            "3 fanti = 6",
            "3 cavalieri = 8",
            "1 per tipo = 10",
            "1 jolly + 2 uguali = 12",
            "+2 per territorio posseduto scambiato",
        ],
    ),
]


def wrap_lines(text: str, width: int):
    return textwrap.wrap(text, width=width, break_long_words=False, break_on_hyphens=False)


def text_block(lines, x, y_start, line_height, size, fill, weight="700"):
    out = []
    y = y_start
    for line in lines:
        out.append(
            f'<text x="{x}" y="{y}" text-anchor="middle" font-size="{size}" fill="{fill}" '
            f'font-family="Trebuchet MS, Arial, sans-serif" font-weight="{weight}">{html.escape(line)}</text>'
        )
        y += line_height
    return "\n  ".join(out)


def objective_svg(card_id: str, title: str, body: str, fallback: str):
    title_lines = wrap_lines(title, 22)
    body_lines = wrap_lines(body, 36)
    fallback_lines = wrap_lines(fallback, 36) if fallback else []
    body_y = 400 if len(body_lines) <= 2 else 388
    fallback_y = 506
    fallback_text = (
        text_block(fallback_lines, 250, fallback_y, 24, 18, "#6f2c2c", "700") if fallback_lines else ""
    )
    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="500" height="700" viewBox="0 0 500 700">
  <defs>
    <linearGradient id="cardBody" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#f8f2e4"/>
      <stop offset="1" stop-color="#eadcc1"/>
    </linearGradient>
    <linearGradient id="brandPanel" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#2f1714"/>
      <stop offset="1" stop-color="#190d0b"/>
    </linearGradient>
    <linearGradient id="paper" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#fff7e7"/>
      <stop offset="1" stop-color="#f2e3c6"/>
    </linearGradient>
    <filter id="shadow" x="-20%" y="-20%" width="140%" height="140%">
      <feDropShadow dx="0" dy="6" stdDeviation="8" flood-color="#000" flood-opacity="0.28"/>
    </filter>
  </defs>
  <rect x="10" y="10" width="480" height="680" rx="30" fill="url(#cardBody)" stroke="#1f1f1f" stroke-width="8" filter="url(#shadow)"/>
  <rect x="18" y="18" width="464" height="664" rx="27" fill="none" stroke="#d0af71" stroke-width="2.1" opacity="0.92"/>

  <rect x="26" y="26" width="448" height="118" rx="22" fill="url(#brandPanel)" stroke="#0f0a08" stroke-width="5"/>
  <text x="250" y="86" text-anchor="middle" dominant-baseline="middle" font-size="62" fill="#ffffff" stroke="#0e0e0e" stroke-width="6" paint-order="stroke fill" font-family="Impact, Arial Black, Trebuchet MS, sans-serif" font-weight="900" letter-spacing="3">OBIETTIVO</text>

  <rect x="50" y="170" width="400" height="420" rx="22" fill="url(#paper)" stroke="#8d7655" stroke-width="3"/>
  <rect x="66" y="188" width="368" height="154" rx="12" fill="#f8eacc" stroke="#ccb07f" stroke-width="1.4"/>
  {text_block(title_lines, 250, 246, 36, 30, "#283646", "800")}

  <rect x="66" y="356" width="368" height="184" rx="12" fill="#f7edd7" stroke="#ccb07f" stroke-width="1.2"/>
  {text_block(body_lines, 250, body_y, 30, 22, "#44586a", "700")}
  {fallback_text}

  <text x="250" y="620" text-anchor="middle" font-size="16" fill="#5b5b5b" font-family="Trebuchet MS, Arial, sans-serif" font-weight="700">ID: {html.escape(card_id)}</text>
  <text x="250" y="656" text-anchor="middle" dominant-baseline="middle" font-size="24" fill="#2d2d2d" font-family="Trebuchet MS, Arial, sans-serif" font-weight="700">RisiKo!</text>
</svg>
"""


def rule_svg(card_id: str, title: str, lines):
    wrapped = []
    for line in lines:
        wrapped.extend(wrap_lines(line, 36))
    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="500" height="700" viewBox="0 0 500 700">
  <defs>
    <linearGradient id="cardBody" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#f3efe4"/>
      <stop offset="1" stop-color="#e7dcc4"/>
    </linearGradient>
    <linearGradient id="brandPanel" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#17252e"/>
      <stop offset="1" stop-color="#101920"/>
    </linearGradient>
    <linearGradient id="paper" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#fff9ea"/>
      <stop offset="1" stop-color="#f5e8cd"/>
    </linearGradient>
  </defs>
  <rect x="10" y="10" width="480" height="680" rx="30" fill="url(#cardBody)" stroke="#1f1f1f" stroke-width="8"/>
  <rect x="18" y="18" width="464" height="664" rx="27" fill="none" stroke="#d0af71" stroke-width="2.1" opacity="0.92"/>
  <rect x="26" y="26" width="448" height="118" rx="22" fill="url(#brandPanel)" stroke="#0f0a08" stroke-width="5"/>
  <text x="250" y="86" text-anchor="middle" dominant-baseline="middle" font-size="58" fill="#ffffff" stroke="#0e0e0e" stroke-width="6" paint-order="stroke fill" font-family="Impact, Arial Black, Trebuchet MS, sans-serif" font-weight="900" letter-spacing="2">REGOLE</text>
  <rect x="50" y="170" width="400" height="420" rx="22" fill="url(#paper)" stroke="#8d7655" stroke-width="3"/>
  <rect x="66" y="188" width="368" height="88" rx="12" fill="#f8eacc" stroke="#ccb07f" stroke-width="1.2"/>
  <text x="250" y="242" text-anchor="middle" font-size="30" fill="#283646" font-family="Trebuchet MS, Arial, sans-serif" font-weight="800">{html.escape(title)}</text>
  <rect x="66" y="294" width="368" height="246" rx="12" fill="#f7edd7" stroke="#ccb07f" stroke-width="1.2"/>
  {text_block(wrapped, 250, 336, 30, 22, "#374a59", "700")}
  <text x="250" y="620" text-anchor="middle" font-size="16" fill="#5b5b5b" font-family="Trebuchet MS, Arial, sans-serif" font-weight="700">ID: {html.escape(card_id)}</text>
  <text x="250" y="656" text-anchor="middle" dominant-baseline="middle" font-size="24" fill="#2d2d2d" font-family="Trebuchet MS, Arial, sans-serif" font-weight="700">RisiKo!</text>
</svg>
"""


def main():
    OBJECTIVE_DIR.mkdir(parents=True, exist_ok=True)
    RULE_DIR.mkdir(parents=True, exist_ok=True)

    for card_id, title, body, fallback in OBJECTIVES:
        svg = objective_svg(card_id, title, body, fallback)
        (OBJECTIVE_DIR / f"{card_id}.svg").write_text(svg, encoding="utf-8")

    for card_id, title, lines in RULE_CARDS:
        svg = rule_svg(card_id, title, lines)
        (RULE_DIR / f"{card_id}.svg").write_text(svg, encoding="utf-8")

    summary = [
        f"objectives={len(OBJECTIVES)}",
        f"rules={len(RULE_CARDS)}",
        f"out={OUT_DIR.as_posix()}",
    ]
    (OUT_DIR / "README.txt").write_text("\n".join(summary) + "\n", encoding="utf-8")
    print("\n".join(summary))


if __name__ == "__main__":
    main()
