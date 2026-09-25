import io
from dataclasses import dataclass, field

import pymupdf
from PIL import Image

from app.config import settings
from app.extraction.errors import ExtractionError
from app.extraction.image import ocr_image

# An embedded-text page counts as "useful" once it has this many characters.
_USEFUL_TOTAL_CHARS = 200


@dataclass
class PdfTextResult:
    text: str = ""
    pages_processed: int = 0
    used_ocr: bool = False
    page_count: int = 0
    per_page: list[str] = field(default_factory=list)


def extract_pdf_text(data: bytes, *, max_pages: int | None = None) -> PdfTextResult:
    """Best-effort text for the first ``max_pages`` pages: embedded text first,
    rendered-page OCR as a fallback."""
    limit = max_pages or settings.max_ocr_pages
    try:
        doc = pymupdf.open(stream=data, filetype="pdf")
    except Exception as exc:
        raise ExtractionError("malformed") from exc

    with doc:
        result = PdfTextResult(page_count=doc.page_count)
        pages_read = min(doc.page_count, limit)

        for index in range(pages_read):
            page_text = doc[index].get_text("text") or ""
            result.per_page.append(page_text)

        useful_chars = sum(len(t.strip()) for t in result.per_page)
        if useful_chars >= min(settings.min_text_chars_per_page * pages_read, _USEFUL_TOTAL_CHARS):
            result.text = "\n".join(result.per_page)
            result.pages_processed = pages_read
            return result

        # Embedded text too thin — OCR each rendered page instead.
        result.used_ocr = True
        ocr_pages: list[str] = []
        for index in range(pages_read):
            pixmap = doc[index].get_pixmap(dpi=settings.ocr_dpi)
            img = Image.open(io.BytesIO(pixmap.tobytes("png")))
            ocr_pages.append(ocr_image(img))
        result.per_page = ocr_pages
        result.text = "\n".join(ocr_pages)
        result.pages_processed = pages_read
        return result
