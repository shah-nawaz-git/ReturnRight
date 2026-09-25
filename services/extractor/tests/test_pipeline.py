from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.extraction.image import tesseract_available
from app.main import app

FIXTURES = Path(__file__).parent / "fixtures"
client = TestClient(app)


def _post(name: str, data: bytes | None = None):
    payload = data if data is not None else (FIXTURES / name).read_bytes()
    response = client.post("/extract", files={"file": (name, payload)})
    assert response.status_code == 200, response.text
    return response.json()


def _field(body: dict, field: str) -> dict | None:
    return next((c for c in body["candidates"] if c["field"] == field), None)


def test_invoice_clean_text_pdf():
    body = _post("invoice_clean.pdf")
    assert body["status"] == "succeeded"
    assert body["used_ocr"] is False

    merchant = _field(body, "merchant")
    assert "soundmarket" in merchant["value"].lower()
    assert merchant["evidence"] == "SOUNDMARKET LTD"

    assert _field(body, "purchase_date")["value"] == "2026-09-10"

    order = _field(body, "order_number")
    assert order["value"] == "SM-48213"
    assert order["confidence"] == 0.7

    assert _field(body, "currency")["value"] == "EUR"

    total = _field(body, "total")
    assert total["value"] == "389.99"
    assert total["confidence"] == 0.85

    assert _field(body, "return_deadline")["value"] == "2026-10-10"

    names = [i["name"] for i in body["items"]]
    assert any("auralis" in n.lower() for n in names)
    item = next(i for i in body["items"] if "auralis" in i["name"].lower())
    assert item["quantity"] == 1
    assert float(item["unit_price"]) == 389.99


@pytest.mark.skipif(not tesseract_available(), reason="tesseract missing")
def test_receipt_scan_png_via_ocr():
    body = _post("receipt_scan.png")
    assert body["status"] in ("succeeded", "partial")
    assert body["used_ocr"] is True

    purchase_date = _field(body, "purchase_date")
    assert purchase_date["value"] == "2026-09-14"
    assert _field(body, "total")["value"] == "79.00"
    assert _field(body, "currency")["value"] == "EUR"
    assert _field(body, "order_number")["value"] == "LC-2026-00417"


@pytest.mark.skipif(not tesseract_available(), reason="tesseract missing")
def test_lowquality_image_best_effort():
    body = _post("receipt_lowquality.jpg")
    assert body["status"] in ("partial", "failed", "succeeded")
    assert all(c["confidence"] <= 0.9 for c in body["candidates"])


def test_multi_item_pdf():
    body = _post("receipt_multi_item.pdf")
    assert body["status"] == "succeeded"

    assert _field(body, "total")["value"] == "220.70"
    assert _field(body, "order_number")["value"] == "NH-77012"
    assert _field(body, "purchase_date")["value"] == "2026-08-30"

    by_name = {i["name"].lower(): i for i in body["items"]}
    linen = next(v for k, v in by_name.items() if "linen cushion" in k)
    table = next(v for k, v in by_name.items() if "side table" in k)
    mug = next(v for k, v in by_name.items() if "ceramic mug" in k)
    assert linen["quantity"] == 2 and float(linen["unit_price"]) == 19.90
    assert table["quantity"] == 1 and float(table["unit_price"]) == 149.00
    assert mug["quantity"] == 4 and float(mug["unit_price"]) == 6.50


def test_no_order_number_means_no_candidate():
    body = _post("receipt_no_order.pdf")
    assert _field(body, "order_number") is None
    assert _field(body, "purchase_date")["value"] == "2026-09-02"


def test_ambiguous_totals_prefers_grand_total():
    body = _post("receipt_ambiguous_totals.pdf")
    assert _field(body, "total")["value"] == "121.00"


def test_malformed_pdf_returns_failed_200():
    body = _post("malformed.pdf")
    assert body["status"] == "failed"
    assert body["message"] == "The PDF could not be opened."


def test_unsupported_and_disguised_types_rejected():
    assert (
        client.post(
            "/extract",
            files={"file": ("unsupported.gif", (FIXTURES / "unsupported.gif").read_bytes())},
        ).status_code
        == 415
    )
    assert (
        client.post(
            "/extract",
            files={"file": ("not_a_pdf.exe.pdf", (FIXTURES / "not_a_pdf.exe.pdf").read_bytes())},
        ).status_code
        == 415
    )


def test_oversize_rejected():
    data = b"%PDF" + b"x" * (10_485_761)
    response = client.post("/extract", files={"file": ("big.pdf", data)})
    assert response.status_code == 413


@pytest.mark.skipif(not tesseract_available(), reason="tesseract missing")
def test_empty_text_pdf_fails_cleanly_via_ocr():
    body = _post("empty_text.pdf")
    assert body["status"] == "failed"
    assert body["message"] == "No readable purchase details were found."
    assert body["used_ocr"] is True
    assert body["pages_processed"] == 1


@pytest.mark.skipif(not tesseract_available(), reason="tesseract missing")
def test_many_pages_caps_ocr_at_five():
    body = _post("many_pages.pdf")
    assert body["pages_processed"] == 5
    assert body["page_count"] == 8


def test_ambiguous_date_fixture():
    body = _post("ambiguous_date.pdf")
    purchase_date = _field(body, "purchase_date")
    assert purchase_date["value"] == "2026-04-03"
    assert purchase_date["confidence"] <= 0.5
