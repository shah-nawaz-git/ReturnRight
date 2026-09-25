import logging
import tempfile
from pathlib import Path

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app

FIXTURES = Path(__file__).parent / "fixtures"
client = TestClient(app)


def _post_invoice(headers: dict | None = None):
    return client.post(
        "/extract",
        files={"file": ("invoice_clean.pdf", (FIXTURES / "invoice_clean.pdf").read_bytes())},
        headers=headers or {},
    )


def test_shared_secret_enforced_when_configured(monkeypatch):
    monkeypatch.setattr(settings, "extractor_shared_secret", "s3cret")
    assert _post_invoice().status_code == 401
    assert _post_invoice({"X-Extractor-Key": "wrong"}).status_code == 401
    assert _post_invoice({"X-Extractor-Key": "s3cret"}).status_code == 200


def test_no_secret_means_open(monkeypatch):
    monkeypatch.setattr(settings, "extractor_shared_secret", None)
    assert _post_invoice().status_code == 200


def test_no_temp_files_left_behind():
    tmp = Path(tempfile.gettempdir())
    before = set(tmp.iterdir())
    assert _post_invoice().status_code == 200
    after = set(tmp.iterdir())
    leftovers = after - before
    assert leftovers == set(), f"temp files leaked: {leftovers}"


def test_logs_never_contain_document_text(caplog):
    with caplog.at_level(logging.INFO, logger="returnright.extractor"):
        assert _post_invoice().status_code == 200
    assert "soundmarket" not in caplog.text.lower()
    assert "SM-48213" not in caplog.text
    assert "389.99" not in caplog.text
