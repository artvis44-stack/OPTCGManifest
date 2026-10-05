#!/bin/sh
set -e
# A fresh data volume gets the catalogue the image was built with; after that the
# volume's copy is kept, so a catalogue refreshed in place is not overwritten.
if [ ! -f "$MANIFEST_ROOT/catalog.json" ]; then
  cp /app/catalog.json "$MANIFEST_ROOT/catalog.json"
fi
exec /app/manifest "$@"
