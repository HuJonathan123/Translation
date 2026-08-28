import json
import os
import re
from functools import lru_cache

from fastapi import FastAPI, HTTPException
from google.api_core import exceptions as google_exceptions
from google.cloud import translate_v3
from google.oauth2 import service_account
from pydantic import BaseModel, Field

MAX_TEXT_LENGTH = 10_000
LANGUAGE_CODE_PATTERN = re.compile(r"^[a-z]{2,3}(?:-[A-Za-z]{2,4})?$")

app = FastAPI(title="Translator API", version="1.0.0")


class TranslationRequest(BaseModel):
    text: str = Field(min_length=1, max_length=MAX_TEXT_LENGTH)
    source_lang: str = "auto"
    target_lang: str = "zh-TW"


class TranslationResponse(BaseModel):
    translated_text: str
    detected_source_lang: str | None = None


@lru_cache
def get_translation_client() -> tuple[translate_v3.TranslationServiceClient, str]:
    credentials_json = os.environ.get("GOOGLE_APPLICATION_CREDENTIALS_JSON")
    if not credentials_json:
        raise RuntimeError("GOOGLE_APPLICATION_CREDENTIALS_JSON is not configured.")

    service_account_info = json.loads(credentials_json)
    project_id = service_account_info.get("project_id")
    if not project_id:
        raise RuntimeError("Google service-account credentials do not contain project_id.")

    credentials = service_account.Credentials.from_service_account_info(
        service_account_info
    )
    return translate_v3.TranslationServiceClient(credentials=credentials), project_id


def validate_language_code(language_code: str, field_name: str) -> None:
    if language_code != "auto" and not LANGUAGE_CODE_PATTERN.fullmatch(language_code):
        raise HTTPException(
            status_code=422,
            detail=f"{field_name} must be 'auto' or a valid BCP-47 language code.",
        )


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/api/translate", response_model=TranslationResponse)
def translate(request: TranslationRequest) -> TranslationResponse:
    validate_language_code(request.source_lang, "source_lang")
    validate_language_code(request.target_lang, "target_lang")
    if request.target_lang == "auto":
        raise HTTPException(status_code=422, detail="target_lang cannot be 'auto'.")

    try:
        client, project_id = get_translation_client()
        response = client.translate_text(
            contents=[request.text],
            target_language_code=request.target_lang,
            source_language_code=(
                None if request.source_lang == "auto" else request.source_lang
            ),
            parent=f"projects/{project_id}/locations/global",
            mime_type="text/plain",
        )
    except (ValueError, RuntimeError, json.JSONDecodeError) as error:
        raise HTTPException(
            status_code=503, detail="Translation service is not configured correctly."
        ) from error
    except (
        google_exceptions.GoogleAPICallError,
        google_exceptions.RetryError,
    ) as error:
        raise HTTPException(
            status_code=502, detail="Translation provider request failed."
        ) from error

    translation = response.translations[0]
    return TranslationResponse(
        translated_text=translation.translated_text,
        detected_source_lang=translation.detected_language_code or None,
    )
