from __future__ import annotations

from typing import Any

import requests

from .config import Settings
from .json_utils import extract_json


class OllamaClient:
    def __init__(self, settings: Settings):
        self.base_url = settings.ollama_base_url
        self.model = settings.ollama_model
        self.keep_alive = settings.ollama_keep_alive

    def chat_json(
        self,
        system: str,
        user: str,
        *,
        temperature: float = 0.1,
        timeout: int = 180,
    ) -> Any:
        response = requests.post(
            f"{self.base_url}/api/chat",
            json={
                "model": self.model,
                "stream": False,
                "format": "json",
                "keep_alive": self.keep_alive,
                "options": {"temperature": temperature},
                "messages": [
                    {"role": "system", "content": system},
                    {"role": "user", "content": user},
                ],
            },
            timeout=timeout,
        )
        response.raise_for_status()
        data = response.json()
        content = data.get("message", {}).get("content", "")
        return extract_json(content)

    def warm_up(self, timeout: int = 90) -> None:
        """Load the configured model before the first interview request.

        Ollama unloads idle models after a short period by default. Keeping the
        model resident makes the first user-visible generation much faster.
        """
        response = requests.post(
            f"{self.base_url}/api/chat",
            json={
                "model": self.model,
                "stream": False,
                "format": "json",
                "keep_alive": self.keep_alive,
                "options": {"num_predict": 1, "temperature": 0},
                "messages": [{"role": "user", "content": "Return {}"}],
            },
            timeout=timeout,
        )
        response.raise_for_status()
