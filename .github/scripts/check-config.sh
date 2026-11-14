#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
python_cmd=python3
"$python_cmd" --version >/dev/null 2>&1 || python_cmd=python
"$python_cmd" - <<'PY'
from pathlib import Path
import sys

tokens = Path('.github/scripts/config-forbidden.txt').read_text().splitlines()
paths = [p for root in ('ISTDocAi.Application', 'ISTDocAi.Infrastructure', 'ISTDocAi.UI') for p in Path(root).rglob('appsettings*.json') if not any(part in ('bin', 'obj') for part in p.parts)] + list(Path('.github').rglob('*'))
failed = False
for path in paths:
    if not path.is_file() or path.as_posix() in ('.github/scripts/config-forbidden.txt', '.github/scripts/check-config.sh'):
        continue
    step = ''
    # Latin-1 preserves ASCII tokens even in legacy-encoded documentation.
    for number, line in enumerate(path.read_text(encoding='latin-1').splitlines(), 1):
        if line.startswith('      - name: '):
            step = line.split(': ', 1)[1]
        # Only this exact runtime-generated connection-string line may contain the password token.
        ci_sql = (path.as_posix() == '.github/workflows/ci.yml' and step == 'Start disposable SQL Server' and line.strip() ==
                  'printf \'DOCAI_TEST_SQL=Server=localhost,1433;User ID=sa;Password=%s;TrustServerCertificate=true;Encrypt=true\\n\' "$sa_secret" >> "$GITHUB_ENV"')
        for token in tokens:
            if token.casefold() in line.casefold() and not (ci_sql and token == 'Password='):
                print(f'{path.as_posix()}:{number}: forbidden configuration pattern ({token})')
                failed = True
sys.exit(1 if failed else 0)
PY
