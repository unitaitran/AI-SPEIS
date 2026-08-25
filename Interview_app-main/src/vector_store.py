from __future__ import annotations

import uuid
from collections.abc import Iterable
from typing import Any

from qdrant_client import QdrantClient, models
from sentence_transformers import SentenceTransformer

from .config import Settings


class VectorStore:
    """Qdrant wrapper for the two reusable question-bank collections."""

    def __init__(self, settings: Settings):
        self.settings = settings
        self.client = QdrantClient(
            url=settings.qdrant_url,
            api_key=settings.qdrant_api_key,
            timeout=120,
        )
        self.embedder = SentenceTransformer(settings.embedding_model)
        size = self.embedder.get_sentence_embedding_dimension()
        if size is None:
            raise RuntimeError("Cannot determine embedding dimension")
        self.vector_size = int(size)

    def ensure_collection(self, name: str, recreate: bool = False) -> None:
        exists = self.client.collection_exists(name)
        if recreate and exists:
            self.client.delete_collection(name)
            exists = False
        if not exists:
            self.client.create_collection(
                collection_name=name,
                vectors_config=models.VectorParams(
                    size=self.vector_size,
                    distance=models.Distance.COSINE,
                ),
            )

    def ensure_keyword_indexes(self, collection: str, fields: Iterable[str]) -> None:
        for field in fields:
            try:
                self.client.create_payload_index(
                    collection_name=collection,
                    field_name=field,
                    field_schema=models.PayloadSchemaType.KEYWORD,
                    wait=True,
                )
            except Exception:
                # Existing indexes and older Qdrant versions may return different errors.
                pass

    def collection_exists(self, collection: str) -> bool:
        return bool(self.client.collection_exists(collection))

    @staticmethod
    def point_id(namespace: str, key: str) -> str:
        return str(uuid.uuid5(uuid.NAMESPACE_URL, f"{namespace}:{key}"))

    def delete_point(self, collection: str, point_id: int | str) -> bool:
        """Idempotently delete a single point ID from a collection."""
        if not self.collection_exists(collection):
            return False
        try:
            self.client.delete(
                collection_name=collection,
                points_selector=models.PointIdsList(points=[point_id]),
                wait=True,
            )
            return True
        except Exception:
            return False

    def delete_points(self, collection: str, point_ids: list[int | str]) -> bool:
        """Idempotently delete multiple point IDs from a collection."""
        if not self.collection_exists(collection) or not point_ids:
            return False
        try:
            self.client.delete(
                collection_name=collection,
                points_selector=models.PointIdsList(points=point_ids),
                wait=True,
            )
            return True
        except Exception:
            return False

    def upsert(self, collection: str, records: list[dict[str, Any]], batch_size: int = 64) -> int:
        total = 0
        for start in range(0, len(records), batch_size):
            batch = records[start : start + batch_size]
            texts = [item["text"] for item in batch]
            vectors = self.embedder.encode(
                texts,
                batch_size=batch_size,
                normalize_embeddings=True,
                show_progress_bar=False,
                convert_to_numpy=True,
            )
            points = []
            for item, vector in zip(batch, vectors):
                payload = {**item["payload"], "text": item["text"]}
                points.append(
                    models.PointStruct(
                        id=item["id"],
                        vector=vector.tolist(),
                        payload=payload,
                    )
                )
            self.client.upsert(collection_name=collection, points=points, wait=True)
            total += len(points)
        return total

    def _encode(self, text: str) -> list[float]:
        return self.embedder.encode(
            [text],
            normalize_embeddings=True,
            convert_to_numpy=True,
            show_progress_bar=False,
        )[0].tolist()

    def query(
        self,
        collection: str,
        text: str,
        limit: int,
        must: dict[str, Any] | None = None,
    ) -> list[dict[str, Any]]:
        """Query Qdrant. Filters are optional and require payload indexes in Qdrant."""
        vector = self._encode(text)
        conditions = [
            models.FieldCondition(key=key, match=models.MatchValue(value=value))
            for key, value in (must or {}).items()
        ]
        query_filter = models.Filter(must=conditions) if conditions else None
        response = self.client.query_points(
            collection_name=collection,
            query=vector,
            query_filter=query_filter,
            limit=limit,
            with_payload=True,
        )
        return [
            {"score": point.score, "payload": point.payload or {}, "id": str(point.id)}
            for point in response.points
        ]

    def query_with_filter_fallback(
        self,
        collection: str,
        text: str,
        limit: int,
        filter_candidates: list[dict[str, Any]] | None = None,
    ) -> list[dict[str, Any]]:
        """Try indexed filters, then automatically retry without filters.

        Existing user collections often have vectors but no keyword payload indexes.
        This method keeps the demo running instead of failing with HTTP 400.
        """
        errors: list[str] = []
        for candidate in filter_candidates or []:
            try:
                hits = self.query(collection, text, limit, must=candidate)
                if hits:
                    return hits
            except Exception as exc:
                errors.append(str(exc))

        try:
            return self.query(collection, text, limit, must=None)
        except Exception as exc:
            detail = str(exc)
            if errors:
                detail += " | Filter attempts: " + " || ".join(errors[-2:])
            raise RuntimeError(
                f"Cannot query Qdrant collection '{collection}': {detail}"
            ) from exc
