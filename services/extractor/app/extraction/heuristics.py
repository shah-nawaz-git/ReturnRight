"""Best-effort purchase-field heuristics over raw document text.

Every candidate carries the source line as ``evidence``; a field that cannot
be evidenced is never fabricated — it is simply absent.
"""

import re
from datetime import date, timedelta

from app.models import FieldCandidate, ItemCandidate

# ---------------------------------------------------------------------------
# Normalization


def _lines(text: str) -> list[str]:
    normalized = text.replace("\r\n", "\n").replace("\r", "\n").replace("\t", " ")
    normalized = re.sub(r" {3,}", "  ", normalized)
    return [line.strip() for line in normalized.split("\n") if line.strip()]


def _evidence(line: str) -> str:
    return line.strip()[:160]


# ---------------------------------------------------------------------------
# Money

_MONEY_BODY = r"(?:\d{1,3}(?:[.,\s]\d{3})*(?:[.,]\d{2})|\d+[.,]\d{2})"
MONEY_RE = re.compile(r"(?:[€£$]\s?)?(" + _MONEY_BODY + r")\s?(?:€|£|\$|EUR|GBP|USD|CHF)?")
_MONEY_ONLY_RE = re.compile(r"^" + _MONEY_BODY + r"$")


def parse_amount(token: str) -> str:
    """'1.234,56' | '1,234.56' | '79,00 EUR' | '389.99' → '1234.56'-style."""
    cleaned = re.sub(r"[^\d.,]", "", token)
    if "." in cleaned and "," in cleaned:
        if cleaned.rfind(",") > cleaned.rfind("."):
            cleaned = cleaned.replace(".", "").replace(",", ".")
        else:
            cleaned = cleaned.replace(",", "")
    elif "," in cleaned:
        head, _, tail = cleaned.rpartition(",")
        if "," not in head and len(tail) == 2:
            cleaned = head.replace(",", "") + "." + tail
        else:
            cleaned = cleaned.replace(",", "")
    try:
        return f"{float(cleaned):.2f}"
    except ValueError:
        return "0.00"


# ---------------------------------------------------------------------------
# Dates

_MONTHS = {
    "jan": 1,
    "january": 1,
    "feb": 2,
    "february": 2,
    "mar": 3,
    "march": 3,
    "apr": 4,
    "april": 4,
    "may": 5,
    "jun": 6,
    "june": 6,
    "jul": 7,
    "july": 7,
    "aug": 8,
    "august": 8,
    "sep": 9,
    "sept": 9,
    "september": 9,
    "oct": 10,
    "october": 10,
    "nov": 11,
    "november": 11,
    "dec": 12,
    "december": 12,
}
_MONTH_ALT = "|".join(sorted(_MONTHS, key=len, reverse=True))

_ISO_DATE_RE = re.compile(r"\b(\d{4})-(\d{2})-(\d{2})\b")
_NUMERIC_DATE_RE = re.compile(r"\b(\d{1,2})[./-](\d{1,2})[./-](\d{2,4})\b")
_DMY_TEXT_RE = re.compile(rf"\b(\d{{1,2}})\s+({_MONTH_ALT})\.?,?\s+(\d{{4}})\b", re.IGNORECASE)
_MDY_TEXT_RE = re.compile(
    rf"\b({_MONTH_ALT})\s+(\d{{1,2}})(?:st|nd|rd|th)?,?\s+(\d{{4}})\b",
    re.IGNORECASE,
)

_DATE_LABEL_RE = re.compile(
    r"(order|purchase|invoice|receipt|payment)\s*date"
    r"|ordered on|purchased on|bought on|\bdate\b",
    re.IGNORECASE,
)
# Lines about future events are never purchase dates.
_DATE_EXCLUDE_RE = re.compile(
    r"\b(deliver\w*|due|expir\w*|warrant\w*|guarantee|returns?)\b", re.IGNORECASE
)

_PURE_DATE_RE = re.compile(r"^\d{1,2}[./-]\d{1,2}[./-]\d{2,4}$|^\d{4}-\d{2}-\d{2}$")


def _plausible(d: date, allow_future: bool = False) -> bool:
    today = date.today()
    if allow_future:
        return d.year >= 2000
    return 2000 <= d.year <= today.year + 1 and d <= today + timedelta(days=1)


def _dates_in_line(line: str, allow_future: bool = False) -> list[tuple[date, bool]]:
    """All plausible dates on a line → (date, ambiguous_numeric)."""
    found: list[tuple[date, bool, int]] = []
    for match in _ISO_DATE_RE.finditer(line):
        try:
            found.append((date(int(match[1]), int(match[2]), int(match[3])), False, match.start()))
        except ValueError:
            continue
    for match in _DMY_TEXT_RE.finditer(line):
        try:
            found.append(
                (
                    date(int(match[3]), _MONTHS[match[2].lower()], int(match[1])),
                    False,
                    match.start(),
                )
            )
        except ValueError:
            continue
    for match in _MDY_TEXT_RE.finditer(line):
        try:
            found.append(
                (
                    date(int(match[3]), _MONTHS[match[1].lower()], int(match[2])),
                    False,
                    match.start(),
                )
            )
        except ValueError:
            continue
    for match in _NUMERIC_DATE_RE.finditer(line):
        a, b, year = int(match[1]), int(match[2]), int(match[3])
        if year < 100:
            year += 2000
        if a > 12 and b <= 12:
            day, month, ambiguous = a, b, False
        elif b > 12 and a <= 12:
            day, month, ambiguous = b, a, False
        else:
            day, month, ambiguous = a, b, True  # EU day-first preference
        try:
            found.append((date(year, month, day), ambiguous, match.start()))
        except ValueError:
            continue
    found.sort(key=lambda f: f[2])
    return [(d, amb) for d, amb, _ in found if _plausible(d, allow_future)]


def find_purchase_date(lines: list[str]) -> FieldCandidate | None:
    unlabelled: tuple[date, bool, str] | None = None
    for line in lines:
        if _DATE_EXCLUDE_RE.search(line):
            continue
        dates = _dates_in_line(line)
        if not dates:
            continue
        if _DATE_LABEL_RE.search(line):
            d, ambiguous = dates[0]
            return FieldCandidate(
                field="purchase_date",
                value=d.isoformat(),
                confidence=0.5 if ambiguous else 0.85,
                evidence=_evidence(line),
            )
        if unlabelled is None:
            unlabelled = (dates[0][0], dates[0][1], line)
    if unlabelled is None:
        return None
    d, ambiguous, line = unlabelled
    return FieldCandidate(
        field="purchase_date",
        value=d.isoformat(),
        confidence=0.5 if ambiguous else 0.55,
        evidence=_evidence(line),
    )


# ---------------------------------------------------------------------------
# Merchant

_MERCHANT_LABEL_RE = re.compile(r"(?i)\b(sold by|merchant|seller)[:\s]+(.+)")
_MERCHANT_KEYWORD_RE = re.compile(
    r"(invoice|receipt|order|date|tax|vat|total|thank|www\.?|http|tel\.?"
    r"|phone|customer|bill to|ship to)|@",
    re.IGNORECASE,
)
_LEGAL_SUFFIX_RE = re.compile(
    r"(\s+(?:ltd|llc|gmbh|inc|b\.v\.|s\.a\.|limited)\.?)+$", re.IGNORECASE
)


def _upper_ratio(word: str) -> float:
    letters = [c for c in word if c.isalpha()]
    if not letters:
        return 0.0
    return sum(1 for c in letters if c.isupper()) / len(letters)


def _is_title_case(word: str) -> bool:
    words = [w for w in word.split() if any(c.isalpha() for c in w)]
    return bool(words) and all(w[0].isupper() for w in words)


def find_merchant(lines: list[str]) -> FieldCandidate | None:
    for line in lines:
        match = _MERCHANT_LABEL_RE.search(line)
        if match:
            value = _LEGAL_SUFFIX_RE.sub("", match[2].strip())
            if len(value) >= 3:
                return FieldCandidate(
                    field="merchant",
                    value=value,
                    confidence=0.9,
                    evidence=_evidence(line),
                )

    def qualifies(line: str) -> bool:
        letters = sum(1 for c in line if c.isalpha())
        nonspace = sum(1 for c in line if not c.isspace())
        return letters >= 3 and letters >= nonspace / 2 and not _MERCHANT_KEYWORD_RE.search(line)

    candidates = [(index, line) for index, line in enumerate(lines[:8]) if qualifies(line)]
    if not candidates:
        return None

    preferred = [
        (i, line) for i, line in candidates if _upper_ratio(line) >= 0.6 or _is_title_case(line)
    ]
    index, raw = preferred[0] if preferred else candidates[0]
    value = _LEGAL_SUFFIX_RE.sub("", raw.strip())
    strong = _upper_ratio(raw) >= 0.6 or index == 0
    return FieldCandidate(
        field="merchant",
        value=value,
        confidence=0.8 if strong else 0.6,
        evidence=_evidence(raw),
    )


# ---------------------------------------------------------------------------
# Order number

_ORDER_LABEL = (
    r"order\s*(?:no\.?|number|#|id)?|invoice\s*(?:no\.?|number|#)?"
    r"|receipt\s*(?:no\.?|number|#)?|reference|ref\.?|transaction|confirmation"
)
_ORDER_RE = re.compile(r"(?i)\b(" + _ORDER_LABEL + r")[\s.#:№-]*([A-Z0-9][A-Z0-9\-/_]{4,29})\b")


def find_order_number(lines: list[str]) -> FieldCandidate | None:
    for line in lines:
        match = _ORDER_RE.search(line)
        if not match:
            continue
        label, value = match[1], match[2]
        if sum(c.isdigit() for c in value) < 2:
            continue
        if _PURE_DATE_RE.match(value) or _MONEY_ONLY_RE.match(value):
            continue
        if re.search(r"order", label, re.IGNORECASE):
            confidence = 0.85
        elif re.search(r"invoice|receipt", label, re.IGNORECASE):
            confidence = 0.7
        else:
            confidence = 0.5
        return FieldCandidate(
            field="order_number",
            value=value,
            confidence=confidence,
            evidence=_evidence(line),
        )
    return None


# ---------------------------------------------------------------------------
# Currency

_CURRENCY_CODE_RE = re.compile(r"\b(EUR|GBP|USD|CHF|SEK|NOK|DKK|PLN|CZK|HUF|CAD|AUD|JPY)\b")
_SYMBOL_MAP = {"€": ("EUR", 0.75), "£": ("GBP", 0.75), "$": ("USD", 0.6)}


def find_currency(lines: list[str]) -> FieldCandidate | None:
    text = "\n".join(lines)
    counts: dict[str, int] = {}
    confidence: dict[str, float] = {}
    for code in _CURRENCY_CODE_RE.findall(text):
        counts[code] = counts.get(code, 0) + 1
        confidence[code] = max(confidence.get(code, 0.0), 0.9)
    for symbol, (code, conf) in _SYMBOL_MAP.items():
        occurrences = text.count(symbol)
        if occurrences:
            counts[code] = counts.get(code, 0) + occurrences
            confidence[code] = max(confidence.get(code, 0.0), conf)
    if not counts:
        return None
    best = max(counts, key=lambda c: counts[c])
    symbol = _symbol_for(best)
    evidence = next(
        (line for line in lines if best in line or (symbol and symbol in line)),
        None,
    )
    return FieldCandidate(
        field="currency",
        value=best,
        confidence=confidence[best],
        evidence=_evidence(evidence) if evidence else None,
    )


def _symbol_for(code: str) -> str:
    return {"EUR": "€", "GBP": "£", "USD": "$"}.get(code, "")


# ---------------------------------------------------------------------------
# Total

_TOTAL_LABEL_RE = re.compile(
    r"\b(grand total|total amount|amount due|total paid|order total"
    r"|total incl\.?|total|amount|balance due|paid)\b",
    re.IGNORECASE,
)
_TOTAL_EXCLUDE_RE = re.compile(
    r"sub\s*-?total|\bvat\b|\btax\b|shipping|discount|before|excl", re.IGNORECASE
)
_TOTAL_STRONG_RE = re.compile(
    r"^(grand total|total amount|amount due|total paid|order total"
    r"|total incl\.?|total)$",
    re.IGNORECASE,
)


def find_total(lines: list[str]) -> FieldCandidate | None:
    labelled: tuple[str, str, bool] | None = None
    for line in lines:
        label = _TOTAL_LABEL_RE.search(line)
        if not label or _TOTAL_EXCLUDE_RE.search(line):
            continue
        amounts = MONEY_RE.findall(line)
        if not amounts:
            continue
        labelled = (amounts[-1], line, bool(_TOTAL_STRONG_RE.match(label[1])))

    if labelled is not None:
        amount, line, strong = labelled
        return FieldCandidate(
            field="total",
            value=parse_amount(amount),
            confidence=0.85 if strong else 0.6,
            evidence=_evidence(line),
        )

    amounts = [(float(parse_amount(m)), m, line) for line in lines for m in MONEY_RE.findall(line)]
    if not amounts:
        return None
    amount, _, line = max(amounts, key=lambda a: a[0])
    return FieldCandidate(
        field="total",
        value=f"{amount:.2f}",
        confidence=0.4,
        evidence=_evidence(line),
    )


# ---------------------------------------------------------------------------
# Return deadline / warranty end

_RETURN_LABEL_RE = re.compile(
    r"returns?\s+(by|before|until|deadline)|return window (ends|closes)"
    r"|eligible for return until",
    re.IGNORECASE,
)
_WARRANTY_LABEL_RE = re.compile(
    r"warranty\s+(valid\s+)?(until|expires|ends|through)"
    r"|guarantee\s+(until|expires)",
    re.IGNORECASE,
)


def _explicit_date_field(
    lines: list[str], label_re: re.Pattern[str], field: str
) -> FieldCandidate | None:
    for line in lines:
        if not label_re.search(line):
            continue
        dates = _dates_in_line(line, allow_future=True)
        if dates:
            return FieldCandidate(
                field=field,
                value=dates[0][0].isoformat(),
                confidence=0.7,
                evidence=_evidence(line),
            )
    return None


# ---------------------------------------------------------------------------
# Items

_MONEY_TOKEN = r"(?:[€£$]\s?)?" + _MONEY_BODY + r"\s?(?:€|£|\$|EUR|GBP|USD|CHF)?"
_ITEM_RES = [
    re.compile(
        r"^(?P<qty>\d{1,3})\s*[x×]?\s+"
        r"(?P<name>[A-Za-z][^€£$]{3,80}?)\s+(?P<price>" + _MONEY_TOKEN + r")$"
    ),
    re.compile(
        r"^(?P<name>[A-Za-z][^€£$]{3,80}?)\s+"
        r"(?P<qty>\d{1,3})\s*[x×]?\s+(?P<price>" + _MONEY_TOKEN + r")$"
    ),
    re.compile(r"^(?P<name>[A-Za-z][^€£$]{3,80}?)\s{2,}(?P<price>" + _MONEY_TOKEN + r")$"),
]
_ITEM_EXCLUDE_RE = re.compile(
    r"\b(sub\s*-?total|total|tax|vat|shipping|discount|delivery|payment"
    r"|card|change|cash|balance)\b",
    re.IGNORECASE,
)
_SKU_TAIL_RE = re.compile(r"(\s+[A-Z0-9][A-Z0-9\-_/]{5,})+$")
_ITEMS_LABEL_RE = re.compile(r"(?im)^\s*items?\s*:?\s+(.+)$")


def find_items(lines: list[str]) -> list[ItemCandidate]:
    items: list[ItemCandidate] = []
    seen: set[str] = set()
    for line in lines:
        for pattern in _ITEM_RES:
            match = pattern.match(line)
            if not match:
                continue
            name = match["name"].strip()
            name = _SKU_TAIL_RE.sub("", name).strip()
            if len(name) < 3 or _ITEM_EXCLUDE_RE.search(name):
                break
            key = name.lower()
            if key in seen:
                break
            seen.add(key)
            qty = match.groupdict().get("qty")
            items.append(
                ItemCandidate(
                    name=name,
                    quantity=int(qty) if qty else None,
                    unit_price=parse_amount(match["price"]),
                    confidence=0.7 if qty else 0.6,
                )
            )
            break
        if len(items) >= 10:
            break

    if not items:
        fallback = _ITEMS_LABEL_RE.search("\n".join(lines))
        if fallback:
            items.append(ItemCandidate(name=fallback[1].strip()[:80], confidence=0.4))
    return items[:10]


# ---------------------------------------------------------------------------
# Public API

_CORE_FIELDS = {"merchant", "purchase_date", "order_number", "total"}


def extract_fields(text: str) -> tuple[list[FieldCandidate], list[ItemCandidate]]:
    """Run every heuristic over normalized text. Absent fields yield no
    candidate — nothing is fabricated."""
    lines = _lines(text)
    if not lines:
        return [], []

    candidates = [
        c
        for c in (
            find_merchant(lines),
            find_purchase_date(lines),
            find_order_number(lines),
            find_currency(lines),
            find_total(lines),
            _explicit_date_field(lines, _RETURN_LABEL_RE, "return_deadline"),
            _explicit_date_field(lines, _WARRANTY_LABEL_RE, "warranty_end"),
        )
        if c is not None
    ]
    return candidates, find_items(lines)
