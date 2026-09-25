from decimal import Decimal
from typing import Literal

from pydantic import BaseModel

CandidateField = Literal[
    "merchant",
    "purchase_date",
    "order_number",
    "currency",
    "total",
    "return_deadline",
    "warranty_end",
]


class FieldCandidate(BaseModel):
    field: CandidateField
    value: str
    confidence: float
    evidence: str | None = None


class ItemCandidate(BaseModel):
    name: str
    quantity: int | None = None
    unit_price: Decimal | None = None
    confidence: float


class ExtractionResult(BaseModel):
    status: Literal["succeeded", "partial", "failed"]
    used_ocr: bool = False
    pages_processed: int = 0
    page_count: int = 0
    candidates: list[FieldCandidate] = []
    items: list[ItemCandidate] = []
    message: str | None = None
