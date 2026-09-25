from app.extraction import heuristics


def _by_field(candidates, field):
    return next((c for c in candidates if c.field == field), None)


def extract(text):
    candidates, items = heuristics.extract_fields(text)
    return candidates, items


def test_merchant_from_sold_by_label():
    candidates, _ = extract("Receipt\nSold by: Acme GmbH\nTotal 10.00 €")
    merchant = _by_field(candidates, "merchant")
    assert merchant is not None
    assert merchant.value == "Acme"
    assert merchant.confidence == 0.9
    assert "Sold by" in merchant.evidence


def test_merchant_from_uppercase_heading_strips_suffix():
    candidates, _ = extract("SOUNDMARKET LTD\nInvoice\nTotal 389.99 €")
    merchant = _by_field(candidates, "merchant")
    assert merchant.value == "SOUNDMARKET"
    assert merchant.confidence == 0.8
    assert merchant.evidence == "SOUNDMARKET LTD"


def test_purchase_date_prefers_labelled_line():
    candidates, _ = extract("ACME\nReference 2026-01-05\nOrder date: 10 Sep 2026\nTotal 9.99 €")
    purchase_date = _by_field(candidates, "purchase_date")
    assert purchase_date.value == "2026-09-10"
    assert purchase_date.confidence == 0.85


def test_unlabelled_date_lower_confidence():
    candidates, _ = extract("ACME\nSomething happened 2026-09-11\nTotal 5.00 €")
    purchase_date = _by_field(candidates, "purchase_date")
    assert purchase_date.value == "2026-09-11"
    assert purchase_date.confidence == 0.55


def test_delivery_date_lines_are_ignored():
    candidates, _ = extract("ACME\nDelivery date: 2026-09-20\nOrder date: 2026-09-10\nTotal 5.00 €")
    purchase_date = _by_field(candidates, "purchase_date")
    assert purchase_date.value == "2026-09-10"


def test_return_by_line_is_not_a_purchase_date():
    candidates, _ = extract("ACME\nReturn by 10 Oct 2026\nTotal 5.00 €")
    assert _by_field(candidates, "purchase_date") is None
    assert _by_field(candidates, "return_deadline").value == "2026-10-10"


def test_ambiguous_numeric_date_prefers_day_first_capped_confidence():
    candidates, _ = extract("ACME\nDate: 03/04/2026\nTotal 5.00 €")
    purchase_date = _by_field(candidates, "purchase_date")
    assert purchase_date.value == "2026-04-03"
    assert purchase_date.confidence <= 0.5


def test_unambiguous_numeric_date():
    candidates, _ = extract("ACME\nDate: 14/04/2026\nTotal 5.00 €")
    purchase_date = _by_field(candidates, "purchase_date")
    assert purchase_date.value == "2026-04-14"
    assert purchase_date.confidence == 0.85


def test_iso_and_textual_month_formats():
    for text, expected in (
        ("Date: 2026-09-14", "2026-09-14"),
        ("Order date: 10 Sep 2026", "2026-09-10"),
        ("Purchased on September 10, 2026", "2026-09-10"),
    ):
        candidates, _ = extract(f"ACME\n{text}\nTotal 5.00 €")
        assert _by_field(candidates, "purchase_date").value == expected


def test_order_number_requires_two_digits():
    candidates, _ = extract("ACME\nOrder: ABCDE\nTotal 5.00 €")
    assert _by_field(candidates, "order_number") is None


def test_order_number_confidence_by_label():
    for line, expected_value, expected_conf in (
        ("Order number: NH-77012", "NH-77012", 0.85),
        ("Invoice number: SM-48213", "SM-48213", 0.7),
        ("Reference: TRX-88421", "TRX-88421", 0.5),
    ):
        candidates, _ = extract(f"ACME\n{line}\nTotal 5.00 €")
        order = _by_field(candidates, "order_number")
        assert order is not None, line
        assert order.value == expected_value
        assert order.confidence == expected_conf


def test_decimal_comma_amount():
    candidates, _ = extract("ACME\nTotal 1.234,56 €")
    total = _by_field(candidates, "total")
    assert total.value == "1234.56"


def test_dollar_currency_is_lower_confidence():
    candidates, _ = extract("ACME\nTotal 25.00 $")
    currency = _by_field(candidates, "currency")
    assert currency.value == "USD"
    assert currency.confidence == 0.6


def test_explicit_currency_code_wins():
    candidates, _ = extract("ACME\nTotal 79.00 EUR\nPaid 79.00 EUR")
    currency = _by_field(candidates, "currency")
    assert currency.value == "EUR"
    assert currency.confidence == 0.9


def test_total_prefers_last_labelled_line():
    candidates, _ = extract(
        "ACME\nSubtotal 100.00 €\nTotal before tax 100.00 €\nTotal 121.00 €\nAmount paid 121.00 €"
    )
    total = _by_field(candidates, "total")
    assert total.value == "121.00"


def test_total_falls_back_to_largest_amount():
    candidates, _ = extract("ACME\nSeen 10.00 € and 42.50 € somewhere")
    total = _by_field(candidates, "total")
    assert total.value == "42.50"
    assert total.confidence == 0.4


def test_empty_text_produces_nothing():
    candidates, items = extract("")
    assert candidates == []
    assert items == []
    candidates, items = extract("   \n\n  ")
    assert candidates == []
    assert items == []


def test_no_hallucination_when_absent():
    candidates, items = extract("Just some words without numbers or dates.")
    assert _by_field(candidates, "order_number") is None
    assert _by_field(candidates, "purchase_date") is None
    assert _by_field(candidates, "total") is None
    assert items == []


def test_items_three_patterns():
    candidates_items_text = (
        "SHOP\n"
        "1 x Plain Widget 10.00 €\n"
        "Second Gadget  2 x  5.00 EUR\n"
        "Third Thingy   7.50 €\n"
        "Total 27.50 €"
    )
    _, items = extract(candidates_items_text)
    names = [i.name for i in items]
    assert "Plain Widget" in names
    assert "Second Gadget" in names
    assert "Third Thingy" in names
    widget = next(i for i in items if i.name == "Plain Widget")
    assert widget.quantity == 1
    assert str(widget.unit_price) == "10.00"
    assert widget.confidence == 0.7
    thingy = next(i for i in items if i.name == "Third Thingy")
    assert thingy.quantity is None
    assert thingy.confidence == 0.6


def test_item_lines_matching_skip_words_ignored():
    _, items = extract("SHOP\nTotal   42.00 €\nShipping   5.90 €")
    assert items == []


def test_items_fallback_label():
    _, items = extract("NOTE\nItems: one broken desk lamp\nTotal 89.00 €")
    assert len(items) == 1
    assert items[0].confidence == 0.4
    assert "lamp" in items[0].name.lower()


def test_return_deadline_never_inferred():
    candidates, _ = extract("ACME\nOrder date: 2026-09-10\nTotal 5.00 €")
    assert _by_field(candidates, "return_deadline") is None
    assert _by_field(candidates, "warranty_end") is None


def test_warranty_end_explicit():
    candidates, _ = extract("ACME\nWarranty until 12 Sep 2028\nTotal 5.00 €")
    warranty = _by_field(candidates, "warranty_end")
    assert warranty.value == "2028-09-12"
    assert warranty.confidence == 0.7
