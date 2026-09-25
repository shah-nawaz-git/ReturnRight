from fastapi.testclient import TestClient

from app.config import settings
from app.main import app

client = TestClient(app)


def test_rejects_oversize_file():
    data = b"%PDF" + b"x" * (settings.max_file_bytes + 1)
    response = client.post("/extract", files={"file": ("big.pdf", data, "application/pdf")})
    assert response.status_code == 413


def test_rejects_unsupported_type():
    response = client.post("/extract", files={"file": ("note.txt", b"hello world", "text/plain")})
    assert response.status_code == 415


def test_malformed_pdf_fails_cleanly():
    response = client.post(
        "/extract", files={"file": ("receipt.pdf", b"%PDF-1.4 fake", "application/pdf")}
    )
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "failed"
    assert body["message"] == "The PDF could not be opened."
