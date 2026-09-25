import io

import pymupdf
import pytest
from PIL import Image, ImageDraw, ImageFont

from app.extraction.errors import ExtractionError
from app.extraction.image import tesseract_available
from app.extraction.pdf import extract_pdf_text

FIXTURES = __import__("pathlib").Path(__file__).parent / "fixtures"


def _text_pdf_bytes(lines: list[str]) -> bytes:
    doc = pymupdf.open()
    page = doc.new_page()
    y = 72
    for line in lines:
        page.insert_text((72, y), line, fontsize=12)
        y += 20
    data = doc.tobytes()
    doc.close()
    return data


def test_embedded_text_path_uses_no_ocr():
    data = _text_pdf_bytes(["ACME STORE", "Order number: 12345", "Total 10.00 €"])
    result = extract_pdf_text(data, max_pages=5)
    assert result.used_ocr is False
    assert result.page_count == 1
    assert result.pages_processed == 1
    assert "ACME STORE" in result.text


def test_malformed_pdf_raises_extraction_error():
    with pytest.raises(ExtractionError) as exc:
        extract_pdf_text(b"%PDF-1.7\n%%garbage\n" + b"x" * 512, max_pages=5)
    assert exc.value.code == "malformed"


def test_page_cap_is_respected():
    data = (FIXTURES / "many_pages.pdf").read_bytes()
    result = extract_pdf_text(data, max_pages=5)
    assert result.page_count == 8
    assert result.pages_processed == 5


@pytest.mark.skipif(not tesseract_available(), reason="tesseract missing")
def test_ocr_fallback_on_image_only_pdf():
    img = Image.new("L", (1200, 400), 255)
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.load_default(size=60)
    except TypeError:
        font = ImageFont.load_default()
    draw.text((40, 120), "OCRFALLBACK 12345", fill=0, font=font)
    buf = io.BytesIO()
    img.save(buf, format="PNG")

    doc = pymupdf.open()
    page = doc.new_page(width=600, height=200)
    page.insert_image(page.rect, stream=buf.getvalue())
    data = doc.tobytes()
    doc.close()

    result = extract_pdf_text(data, max_pages=5)
    assert result.used_ocr is True
    assert "OCRFALLBACK" in result.text.upper()
