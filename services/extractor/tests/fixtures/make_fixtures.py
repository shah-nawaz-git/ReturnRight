"""Generate the synthetic extraction fixtures next to this script.

All content is fictional (no real PII or brands). Run from the repo:

    python tests/fixtures/make_fixtures.py
"""

import random
from pathlib import Path

import pymupdf
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).parent


def _text_pdf(name: str, lines: list[str], page_count: int = 1, text_page: int = 0) -> None:
    doc = pymupdf.open()
    for page_index in range(page_count):
        page = doc.new_page()
        if page_index == text_page:
            y = 72
            for line in lines:
                page.insert_text((72, y), line, fontsize=12)
                y += 20
    doc.save(HERE / name)
    doc.close()


def _font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    try:
        return ImageFont.truetype("DejaVuSans.ttf", size)
    except OSError:
        return ImageFont.load_default(size=size)


def _scanned_image(
    name: str,
    lines: list[str],
    size: tuple[int, int],
    font_size: int,
    rotation: float = 0.0,
    noise: float = 0.0,
    blur: float = 0.0,
    jpeg_quality: int | None = None,
) -> None:
    img = Image.new("L", size, 255)
    draw = ImageDraw.Draw(img)
    font = _font(font_size)
    y = 60
    for line in lines:
        draw.text((50, y), line, fill=0, font=font)
        y += font_size + 16
    if rotation:
        img = img.rotate(rotation, resample=Image.Resampling.BICUBIC, fillcolor=255)
    if noise:
        noise_img = Image.effect_noise(size, 40)
        img = Image.blend(img, noise_img, noise)
    if blur:
        img = img.filter(ImageFilter.GaussianBlur(blur))
    if jpeg_quality is not None:
        img.convert("RGB").save(HERE / name, quality=jpeg_quality)
    else:
        img.save(HERE / name)


def main() -> None:
    rng = random.Random(42)
    _ = rng  # deterministic placeholder for future jitter

    _text_pdf(
        "invoice_clean.pdf",
        [
            "SOUNDMARKET LTD",
            "Invoice",
            "Invoice number: SM-48213",
            "Order date: 10 Sep 2026",
            "Sold to: Test Customer",
            "1 x Auralis X4 Headphones 389.99 EUR",
            "Subtotal 389.99 EUR",
            "VAT 0.00 EUR",
            "Total 389.99 EUR",
            "Return by 10 Oct 2026",
        ],
    )

    _scanned_image(
        "receipt_scan.png",
        [
            "LUMEN & CO",
            "Receipt",
            "Order #: LC-2026-00417",
            "Date: 2026-09-14",
            "Nordlys Desk Lamp   1   79.00 EUR",
            "Total   79.00 EUR",
        ],
        size=(1400, 2000),
        font_size=36,
        rotation=1.5,
        noise=0.04,
    )

    _scanned_image(
        "receipt_lowquality.jpg",
        [
            "LUMEN & CO",
            "Receipt",
            "Order #: LC-2026-00417",
            "Date: 2026-09-14",
            "Nordlys Desk Lamp   1   79.00 EUR",
            "Total   79.00 EUR",
        ],
        size=(500, 700),
        font_size=20,
        rotation=2.0,
        noise=0.12,
        blur=1.6,
        jpeg_quality=20,
    )

    _text_pdf(
        "receipt_multi_item.pdf",
        [
            "NORDIC HOME STORE",
            "Receipt",
            "Order number: NH-77012",
            "Purchase date: 2026-08-30",
            "2 x Linen Cushion 19.90 EUR",
            "1 x Oak Side Table 149.00 EUR",
            "4 x Ceramic Mug 6.50 EUR",
            "Subtotal 214.80 EUR",
            "Shipping 5.90 EUR",
            "Total 220.70 EUR",
        ],
    )

    _text_pdf(
        "receipt_no_order.pdf",
        [
            "HARBOUR SUPPLY",
            "Receipt",
            "Purchase date: 2026-09-02",
            "1 x Cotton Towel 12.50 EUR",
            "Total 12.50 EUR",
        ],
    )

    _text_pdf(
        "receipt_ambiguous_totals.pdf",
        [
            "KLAR MARKET",
            "Receipt",
            "Subtotal 100.00 EUR",
            "Total before tax 100.00 EUR",
            "Total 121.00 EUR",
            "Amount paid 121.00 EUR",
        ],
    )

    (HERE / "malformed.pdf").write_bytes(
        b"%PDF-1.7\n%%garbage\n" + bytes(rng.randrange(0, 256) for _ in range(512))
    )

    (HERE / "unsupported.gif").write_bytes(
        b"GIF89a\x01\x00\x01\x00\x80\x00\x00\x00\x00\x00\xff\xff\xff"
        b"!\xf9\x04\x01\x00\x00\x00\x00,\x00\x00\x00\x00\x01\x00\x01\x00\x00"
        b"\x02\x02D\x01\x00;"
    )
    (HERE / "not_a_pdf.exe.pdf").write_bytes(b"MZ\x90\x00" + b"\x00" * 256)

    _text_pdf("empty_text.pdf", [], page_count=1)
    _text_pdf(
        "many_pages.pdf",
        ["ORDER 99881", "Total 42.00 EUR"],
        page_count=8,
        text_page=6,
    )

    _text_pdf(
        "ambiguous_date.pdf",
        [
            "ACME PARTS",
            "Receipt",
            "Date: 03/04/2026",
            "Total 25.00 EUR",
        ],
    )

    print("fixtures written to", HERE)


if __name__ == "__main__":
    main()
