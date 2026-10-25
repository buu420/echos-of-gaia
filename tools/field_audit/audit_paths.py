"""Paths supplied by Export-FieldNavigationAudit.py before importing decoders."""
import os
from pathlib import Path

SOURCE_ROOT = Path(__file__).resolve().parents[2]
GAME_ROOT = Path(os.environ['FF9_AUDIT_GAME_ROOT']).resolve()
OUTPUT_ROOT = Path(os.environ['FF9_AUDIT_OUTPUT_ROOT']).resolve()
UNITYPY = os.environ.get('FF9_AUDIT_UNITYPY_ROOT', '')
