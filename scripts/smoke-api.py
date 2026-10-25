#!/usr/bin/env python3
"""Read-only smoke checks against a running Development API with unchanged demo data."""
import json
import sys
import time
import urllib.error
import urllib.request
from decimal import Decimal

base_url = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080").rstrip("/")
deadline = time.monotonic() + 30
while True:
    try:
        with urllib.request.urlopen(base_url + "/health/live", timeout=2) as response:
            assert response.status == 200 and response.read() == b"Healthy"
        break
    except (OSError, AssertionError):
        if time.monotonic() >= deadline:
            raise
        time.sleep(0.5)

for suffix in ["", "?page=1&pageSize=10"]:
    with urllib.request.urlopen(base_url + "/api/v1/products" + suffix, timeout=10) as response:
        assert response.status == 200
        assert response.headers.get_content_type() == "application/json"
        assert "no-store" in response.headers["Cache-Control"]
        products = json.load(response, parse_float=Decimal)
    assert [p["id"] for p in products] == list(range(1, 24))
    for product in products:
        number = product["id"]
        assert set(product) == {"id", "name", "imgUri", "price", "description"}
        assert product["name"] == f"Demo product {number:02}"
        assert product["imgUri"] == f"https://example.com/products/{number}.jpg"
        assert product["price"] == number * Decimal("10.25")
        assert product["description"] == (None if number % 3 == 0 else f"Description for demo product {number:02}.")

with urllib.request.urlopen(base_url + "/swagger", timeout=10) as response:
    assert "/openapi/v1.json" in response.read().decode()
with urllib.request.urlopen(base_url + "/openapi/v1.json", timeout=10) as response:
    document = json.load(response)
    operation = document["paths"]["/api/v1/products"]["get"]
    assert operation["operationId"] == "ListAllProductsV1"
    assert operation["responses"]["200"]["content"]["application/json"]["schema"]["type"] == "array"
try:
    urllib.request.urlopen(base_url + "/does-not-exist", timeout=10)
    raise AssertionError("Unknown route unexpectedly succeeded")
except urllib.error.HTTPError as error:
    assert error.code == 404
    assert error.headers.get_content_type() == "application/problem+json"
    assert json.load(error)["traceId"]

print("Smoke passed: complete 23-product catalog, exact values, no v1 pagination, health, Swagger, OpenAPI and 404.")
