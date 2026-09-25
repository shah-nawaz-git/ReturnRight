from typing import Annotated

from fastapi import Depends, FastAPI, File, Header, HTTPException, UploadFile
from fastapi.concurrency import run_in_threadpool

from app.config import settings
from app.extraction import pipeline
from app.extraction.detect import detect_kind
from app.extraction.image import tesseract_available
from app.models import ExtractionResult

app = FastAPI(title="ReturnRight Extractor", version="0.1.0")


async def require_extractor_key(
    x_extractor_key: Annotated[str | None, Header()] = None,
) -> None:
    """Optional shared-secret gate — enforced only when configured."""
    if settings.extractor_shared_secret and x_extractor_key != settings.extractor_shared_secret:
        raise HTTPException(status_code=401, detail="invalid extractor key")


@app.get("/health")
def health() -> dict:
    return {"status": "ok", "tesseract_available": tesseract_available()}


@app.post("/extract", dependencies=[Depends(require_extractor_key)])
async def extract(file: Annotated[UploadFile, File()]) -> ExtractionResult:
    data = await file.read()

    if len(data) > settings.max_file_bytes:
        raise HTTPException(
            status_code=413,
            detail=f"file exceeds max size of {settings.max_file_bytes} bytes",
        )

    if detect_kind(data) is None:
        raise HTTPException(
            status_code=415,
            detail="unsupported file type (expected pdf, jpeg, or png)",
        )

    return await run_in_threadpool(pipeline.extract, data, file.filename or "file")
