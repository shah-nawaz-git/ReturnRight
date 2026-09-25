import logging
import time

import pytesseract

from app.extraction import heuristics
from app.extraction.detect import detect_kind
from app.extraction.errors import ExtractionError
from app.extraction.image import load_image, ocr_image
from app.extraction.pdf import extract_pdf_text
from app.models import ExtractionResult

logger = logging.getLogger("returnright.extractor")

_CORE_FIELDS = {"merchant", "purchase_date", "order_number", "total"}


def _failed(message: str, **kwargs) -> ExtractionResult:
    return ExtractionResult(status="failed", message=message, **kwargs)


def extract(data: bytes, filename: str) -> ExtractionResult:
    """Single responsibility: one upload → best-effort field candidates."""
    started = time.perf_counter()
    kind = detect_kind(data)
    if kind is None:
        return _failed("Unsupported file type.")

    try:
        if kind == "pdf":
            pdf = extract_pdf_text(data)
            text = pdf.text
            used_ocr = pdf.used_ocr
            pages_processed = pdf.pages_processed
            page_count = pdf.page_count
        else:
            text = ocr_image(load_image(data))
            used_ocr = True
            pages_processed = 1
            page_count = 1
    except ExtractionError as exc:
        message = {
            "malformed": "The PDF could not be opened."
            if kind == "pdf"
            else "The image could not be opened.",
            "too_large": "The document is too large to process.",
        }.get(exc.code, "The document could not be read.")
        logger.info("extract failed kind=%s code=%s", kind, exc.code)
        return _failed(message)
    except pytesseract.pytesseract.TesseractNotFoundError:
        logger.info("extract failed kind=%s tesseract_missing", kind)
        return _failed("Text recognition isn't available.")
    except RuntimeError as exc:
        if "timeout" in str(exc).lower() or "timed out" in str(exc).lower():
            logger.info("extract failed kind=%s ocr_timeout", kind)
            return _failed("Reading the document took too long.")
        raise

    candidates, items = heuristics.extract_fields(text)
    found = {c.field for c in candidates}

    if len(found & _CORE_FIELDS) >= 3:
        status = "succeeded"
        message = None
    elif candidates or items:
        status = "partial"
        message = None
    else:
        status = "failed"
        message = "No readable purchase details were found."

    elapsed_ms = round((time.perf_counter() - started) * 1000)
    logger.info(
        "extract kind=%s bytes=%d pages=%d processed=%d ocr=%s status=%s fields=%s items=%d ms=%d",
        kind,
        len(data),
        page_count,
        pages_processed,
        used_ocr,
        status,
        sorted(found),
        len(items),
        elapsed_ms,
    )
    return ExtractionResult(
        status=status,
        used_ocr=used_ocr,
        pages_processed=pages_processed,
        page_count=page_count,
        candidates=candidates,
        items=items,
        message=message,
    )
