from pathlib import Path

def ico(paths: str, label: str) -> str:
    return (
        f'<svg class="btn-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" '
        f'stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">'
        f"{paths}</svg><span class=\"btn-label\">{label}</span>"
    )

X = '<path d="M18 6 6 18"/><path d="m6 6 12 12"/>'
X_CIRCLE = '<circle cx="12" cy="12" r="10"/><path d="m15 9-6 6"/><path d="m9 9 6 6"/>'
CHECK = '<path d="M20 6 9 17l-5-5"/>'
PENCIL = '<path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z"/>'
BAN = '<circle cx="12" cy="12" r="10"/><path d="m4.9 4.9 14.2 14.2"/>'
CAL = '<path d="M8 2v4"/><path d="M16 2v4"/><rect width="18" height="18" x="3" y="4" rx="2"/><path d="M3 10h18"/>'
PLAY = '<polygon points="5 3 19 12 5 21 5 3"/>'
UNDO = '<path d="M3 7v6h6"/><path d="M3 13a9 9 0 1 0 3-7.7L3 8"/>'
TARGET = '<circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="6"/><circle cx="12" cy="12" r="2"/>'
BAG = '<path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z"/><path d="M3 6h18"/><path d="M16 10a4 4 0 0 1-8 0"/>'
LEFT = '<path d="m12 19-7-7 7-7"/><path d="M19 12H5"/>'
TABLE = '<rect width="18" height="18" x="3" y="3" rx="2"/><path d="M3 9h18"/><path d="M3 15h18"/><path d="M9 3v18"/>'

index = Path(r"src/EShooting.Web/Views/Home/Index.cshtml")
text = index.read_text(encoding="utf-8")

subs = [
    (
        'class="btn btn-ghost">Ləğv et</button>',
        f'class="btn btn-ghost">{ico(X_CIRCLE, "Ləğv et")}</button>',
    ),
    (
        'class="btn btn-primary">Təsdiqlə və saxla</button>',
        f'class="btn btn-primary">{ico(CHECK, "Təsdiqlə və saxla")}</button>',
    ),
    (
        'class="btn btn-primary btn-sm">Dəyişdir</button>',
        f'class="btn btn-primary btn-sm">{ico(PENCIL, "Dəyişdir")}</button>',
    ),
    (
        'class="btn btn-ghost btn-sm" style="display:none;">Kartı qaytar</button>',
        f'class="btn btn-return btn-sm" style="display:none;">{ico(UNDO, "Kartı qaytar")}</button>',
    ),
    (
        'class="btn btn-danger btn-sm">Abunəni ləğv et</button>',
        f'class="btn btn-danger btn-sm">{ico(BAN, "Abunəni ləğv et")}</button>',
    ),
    (
        'class="btn btn-sm btn-info-days-toggle">Gəldiyi günlər</button>',
        f'class="btn btn-sm btn-info-days-toggle">{ico(CAL, "Gəldiyi günlər")}</button>',
    ),
    (
        'class="btn btn-primary" disabled>Təsdiqlə</button>',
        f'class="btn btn-primary" disabled>{ico(CHECK, "Təsdiqlə")}</button>',
    ),
    (
        'class="btn btn-primary btn-sm">Təsdiqlə</button>',
        f'class="btn btn-primary btn-sm">{ico(CHECK, "Təsdiqlə")}</button>',
    ),
    (
        'class="btn btn-ghost">Yadda saxla</button>',
        f'class="btn btn-ghost">{ico(CHECK, "Yadda saxla")}</button>',
    ),
    (
        'class="btn btn-primary">Yadda saxla</button>',
        f'class="btn btn-primary">{ico(CHECK, "Yadda saxla")}</button>',
    ),
]

# JS templates
js_subs = [
    (
        '<button type="button" class="btn btn-ghost btn-sm" data-return-club-card="${escapeHtml(h.athleteId || "")}">Qaytar</button>',
        '<button type="button" class="btn btn-return btn-sm" data-return-club-card="${escapeHtml(h.athleteId || "")}">'
        + ico(UNDO, "Qaytar")
        + "</button>",
    ),
    (
        '<button type="button" class="btn btn-ghost btn-sm" data-keep-club-card>Qalsın</button>',
        '<button type="button" class="btn btn-keep btn-sm" data-keep-club-card>'
        + ico(CHECK, "Qalsın")
        + "</button>",
    ),
    (
        '">Trenajora yaz</button>`',
        '">' + ico(TARGET, "Trenajora yaz") + "</button>`",
    ),
    (
        '">Aktiv et</button>',
        '">' + ico(PLAY, "Aktiv et") + "</button>",
    ),
    (
        'data-cancel-session="${escapeHtml2(x.sessionId)}">Ləğv et</button>',
        'data-cancel-session="${escapeHtml2(x.sessionId)}">' + ico(X_CIRCLE, "Ləğv et") + "</button>",
    ),
    (
        '">Dəyişdir</button>',
        '">' + ico(PENCIL, "Dəyişdir") + "</button>",
    ),
    (
        'data-exclude-occurrence-date="${d}">Ləğv et</button>`',
        'data-exclude-occurrence-date="${d}">' + ico(X_CIRCLE, "Ləğv et") + "</button>`",
    ),
]

for old, new in subs + js_subs:
    count = text.count(old)
    print(count, "match")
    if count == 0:
        continue
    text = text.replace(old, new)

# textContent updates
text = text.replace(
    'customerModalSaveBtn.textContent = "Təsdiqlə və saxla";',
    'setButtonLabel(customerModalSaveBtn, "Təsdiqlə və saxla");',
)
text = text.replace(
    'infoManageEditBtn.textContent = "Saxlanır…";',
    'setButtonLabel(infoManageEditBtn, "Saxlanır…");',
)
text = text.replace(
    'infoManageEditBtn.textContent = "Dəyişdir";',
    'setButtonLabel(infoManageEditBtn, "Dəyişdir");',
)
text = text.replace(
    'infoReturnClubCardBtn.textContent = "Qaytarılır…";',
    'setButtonLabel(infoReturnClubCardBtn, "Qaytarılır…");',
)
text = text.replace(
    'infoReturnClubCardBtn.textContent = "Kartı qaytar";',
    'setButtonLabel(infoReturnClubCardBtn, "Kartı qaytar");',
)

index.write_text(text, encoding="utf-8")
print("Index written")

# infoDaysToggleBtn uses textContent for two labels - patch function
# handled separately if needed
