"""Optional helper: create keyword indexes for the two existing collections.

The application can run without these indexes because it automatically retries
without filters. Creating them improves filter speed and precision.
"""
from qdrant_client import QdrantClient, models

from src.config import get_settings

settings = get_settings()
client = QdrantClient(
    url=settings.qdrant_url,
    api_key=settings.qdrant_api_key,
    timeout=120,
)

for collection in (settings.technical_collection, settings.behavioral_collection):
    if not client.collection_exists(collection):
        print(f"SKIP: collection does not exist: {collection}")
        continue
    for field in ("language", "difficulty"):
        try:
            client.create_payload_index(
                collection_name=collection,
                field_name=field,
                field_schema=models.PayloadSchemaType.KEYWORD,
                wait=True,
            )
            print(f"OK: {collection}.{field}")
        except Exception as exc:
            print(f"SKIP/EXISTS: {collection}.{field}: {exc}")
