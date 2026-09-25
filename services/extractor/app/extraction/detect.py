_MAGIC: dict[str, bytes] = {
    "pdf": b"%PDF",
    "jpeg": b"\xff\xd8\xff",
    "png": b"\x89PNG\r\n\x1a\n",
}


def detect_kind(data: bytes) -> str | None:
    """Sniff magic bytes → 'pdf' | 'jpeg' | 'png' | None."""
    for kind, magic in _MAGIC.items():
        if data.startswith(magic):
            return kind
    return None
