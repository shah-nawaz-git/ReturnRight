import io

import pytest
from PIL import Image

from app.extraction import pipeline
from app.extraction.errors import ExtractionError
from app.extraction.image import load_image


def _png(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def test_load_image_applies_exif_transpose():
    img = Image.new("L", (300, 500), 200)
    exif = img.getexif()
    exif[274] = 6  # rotate 90° CW
    buf = io.BytesIO()
    img.save(buf, format="JPEG", exif=exif)
    loaded = load_image(buf.getvalue())
    assert loaded.width > loaded.height


def test_small_image_is_upscaled():
    data = _png(Image.new("L", (400, 300), 220))
    loaded = load_image(data)
    assert loaded.width == 800
    assert loaded.height == 600


def test_oversized_image_rejected():
    data = _png(Image.new("1", (7000, 6500), 1))
    with pytest.raises(ExtractionError) as exc:
        load_image(data)
    assert exc.value.code == "too_large"


def test_ocr_timeout_maps_to_failed_status(monkeypatch):
    def _timeout(*args, **kwargs):
        raise RuntimeError("Tesseract process timeout")

    monkeypatch.setattr("app.extraction.image.pytesseract.image_to_string", _timeout)
    data = _png(Image.new("L", (400, 300), 220))
    result = pipeline.extract(data, "receipt.png")
    assert result.status == "failed"
    assert result.message == "Reading the document took too long."
