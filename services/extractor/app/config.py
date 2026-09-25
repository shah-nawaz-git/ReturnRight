from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    tesseract_cmd: str = "tesseract"
    max_file_bytes: int = 10_485_760
    max_ocr_pages: int = 5
    ocr_timeout_seconds: int = 20
    min_text_chars_per_page: int = 40
    ocr_dpi: int = 200
    extractor_shared_secret: str | None = None


settings = Settings()
