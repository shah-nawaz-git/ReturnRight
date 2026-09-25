import os
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

# Local Tesseract fallback — set before app.config is imported.
_WINDOWS_TESSERACT = Path(r"C:\Users\hp\tools\rr\tesseract\tesseract.exe")
if (
    "TESSERACT_CMD" not in os.environ
    and shutil.which("tesseract") is None
    and _WINDOWS_TESSERACT.exists()
):
    os.environ["TESSERACT_CMD"] = str(_WINDOWS_TESSERACT)
