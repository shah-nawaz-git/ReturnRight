class ExtractionError(Exception):
    """Document could not be read. ``code`` maps to a user-facing message."""

    def __init__(self, code: str):
        super().__init__(code)
        self.code = code
