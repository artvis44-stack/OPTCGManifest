#!/bin/sh
# Alertmanager for the `monitoring` profile, configured from the environment so
# the SMTP password stays in .env and out of the repository. With
# MANIFEST_ALERT_EMAIL unset, alerts are only shown at its web UI.
set -e
if [ -n "$MANIFEST_ALERT_EMAIL" ] && [ -n "$MANIFEST_SMTP_HOST" ]; then
  receiver=email
  # 465 is TLS from the first byte; anything else upgrades with STARTTLS.
  cat > /tmp/receiver.yml <<YAML
  - name: email
    email_configs:
      - to: "$MANIFEST_ALERT_EMAIL"
        from: "${MANIFEST_SMTP_FROM:-$MANIFEST_SMTP_USER}"
        smarthost: "$MANIFEST_SMTP_HOST:${MANIFEST_SMTP_PORT:-587}"
        auth_username: "$MANIFEST_SMTP_USER"
        auth_password: "$MANIFEST_SMTP_PASSWORD"
        require_tls: $( [ "${MANIFEST_SMTP_PORT:-587}" = 465 ] && echo false || echo true )
        send_resolved: true
YAML
else
  receiver=none
  echo "alertmanager: MANIFEST_ALERT_EMAIL or MANIFEST_SMTP_HOST unset; alerts go nowhere but the UI" >&2
  : > /tmp/receiver.yml
fi

cat > /tmp/alertmanager.yml <<YAML
route:
  receiver: $receiver
  group_by: [alertname]
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 4h
  routes:
    - matchers: ['severity="critical"']
      receiver: $receiver
      repeat_interval: 1h
receivers:
  - name: none
$(cat /tmp/receiver.yml)
YAML

exec /bin/alertmanager --config.file=/tmp/alertmanager.yml --storage.path=/alertmanager "$@"
