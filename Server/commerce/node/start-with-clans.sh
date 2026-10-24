#!/usr/bin/env bash
set -euo pipefail
cd /app
social_pid=''
node_pid=''
cleanup() {
  if [[ -n "$node_pid" ]]; then kill "$node_pid" 2>/dev/null || true; fi
  if [[ -n "$social_pid" ]]; then kill "$social_pid" 2>/dev/null || true; fi
}
trap cleanup EXIT
trap 'exit 143' TERM
trap 'exit 130' INT
/opt/df-social/bin/gunicorn --chdir /app/Server/commerce --bind 127.0.0.1:8081 \
  --workers 1 --threads 4 --timeout 30 node_social_wsgi:application &
social_pid=$!
# Start the public account endpoint only once namespace/configuration checks pass.
/opt/df-social/bin/python - <<'PY'
import time
import urllib.request
for attempt in range(60):
    try:
        with urllib.request.urlopen('http://127.0.0.1:8081/internal/health',timeout=1) as response:
            if response.status==200:
                break
    except Exception:
        time.sleep(.5)
else:
    raise SystemExit('Clan runtime failed readiness; public account endpoint was not started.')
PY
node node_modules/@google-cloud/functions-framework/build/src/main.js --target=helloHttp &
node_pid=$!
# Losing either process restarts the whole Cloud Run instance, never a half-service.
set +e
wait -n "$social_pid" "$node_pid"
status=$?
set -e
if [[ "$status" -eq 0 ]]; then status=1; fi
exit "$status"
