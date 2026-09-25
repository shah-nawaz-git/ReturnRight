import functools
import io
import shutil

import pytesseract
from PIL import Image, ImageOps, UnidentifiedImageError

from app.config import settings
from app.extraction.errors import ExtractionError

_MAX_PIXELS = 40_000_000
_MIN_SIDE_FOR_OCR = 1000
_MAX_SIDE = 3000


def _apply_tesseract_cmd() -> None:
    if settings.tesseract_cmd:
        pytesseract.pytesseract.tesseract_cmd = settings.tesseract_cmd


def load_image(data: bytes) -> Image.Image:
    """Validate + normalize an uploaded image for OCR."""
    try:
        probe = Image.open(io.BytesIO(data))
        probe.verify()
        img = Image.open(io.BytesIO(data))
    except (UnidentifiedImageError, OSError, ValueError) as exc:
        raise ExtractionError("malformed") from exc

    if img.width * img.height > _MAX_PIXELS:
        raise ExtractionError("too_large")

    img = ImageOps.exif_transpose(img)
    img = img.convert("L")

    if min(img.width, img.height) < _MIN_SIDE_FOR_OCR:
        scale = min(2.0, _MAX_SIDE / min(img.width, img.height))
        new_size = (round(img.width * scale), round(img.height * scale))
        img = img.resize(new_size, Image.Resampling.LANCZOS)

    return ImageOps.autocontrast(img)


def ocr_image(img: Image.Image, timeout: int | None = None) -> str:
    """Run Tesseract on a prepared image. Caller maps errors to statuses."""
    _apply_tesseract_cmd()
    return pytesseract.image_to_string(
        img,
        lang="eng",
        config="--psm 6",
        timeout=timeout or settings.ocr_timeout_seconds,
    )


@functools.lru_cache(maxsize=1)
def tesseract_available() -> bool:
    _apply_tesseract_cmd()
    return shutil.which(pytesseract.pytesseract.tesseract_cmd) is not None
