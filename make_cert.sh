#!/bin/sh
# Make a certificate your phone will actually accept.
#
# Phones no longer accept a bare self-signed certificate. This creates two
# things: a small certificate authority of your own, and a server certificate
# signed by it. You install the authority on your phone once; after that its
# certificates are trusted, with no warnings.
#
#   ./make_cert.sh
#   dotnet run --project Manifest -- --https
#
# Then open the /setup page the server prints, on your phone.
#
# Works with OpenSSL and with the LibreSSL that ships on macOS. Uses a config
# file rather than -addext, which LibreSSL does not support.

set -e

CA_DAYS=3650      # the authority can be long-lived
LEAF_DAYS=397     # Safari refuses server certificates valid beyond 398 days

IPS=$(
  { ip -4 -o addr show 2>/dev/null | awk '{print $4}' | cut -d/ -f1
    ifconfig 2>/dev/null | awk '/inet /{print $2}' | sed 's/addr://'
  } | sort -u | grep -Ev '^(127\.|169\.254\.)' || true
)
IPS="$IPS 127.0.0.1"

if [ -z "$(echo "$IPS" | tr -d ' ')" ]; then
  echo "Could not work out this machine's address." >&2
  exit 1
fi

echo "Issuing a certificate for:"
for ip in $IPS; do echo "   $ip"; done

CONF=$(mktemp); CACONF=$(mktemp)
{
  echo "[req]"; echo "distinguished_name = dn"; echo "prompt = no"
  echo "[dn]";  echo "CN = Manifest"
  echo "[ext]"
  echo "basicConstraints = critical, CA:FALSE"
  echo "keyUsage = critical, digitalSignature, keyEncipherment"
  echo "extendedKeyUsage = serverAuth"
  echo "subjectAltName = @alt"
  echo "[alt]"; echo "DNS.1 = localhost"
  i=1; for ip in $IPS; do echo "IP.$i = $ip"; i=$((i + 1)); done
} > "$CONF"
{
  echo "[req]"; echo "distinguished_name = dn"; echo "prompt = no"
  echo "x509_extensions = ext"
  echo "[dn]";  echo "CN = Manifest Local Authority"; echo "O = Manifest"
  echo "[ext]"
  echo "basicConstraints = critical, CA:TRUE, pathlen:0"
  echo "keyUsage = critical, keyCertSign, cRLSign"
} > "$CACONF"

if [ -f ca.pem ] && [ -f ca-key.pem ]; then
  echo "Reusing your existing authority (ca.pem) - no need to reinstall it."
else
  echo "Creating your local certificate authority..."
  openssl req -x509 -newkey rsa:2048 -nodes -days "$CA_DAYS" \
    -keyout ca-key.pem -out ca.pem -config "$CACONF"
  chmod 600 ca-key.pem
fi

echo "Creating the server certificate..."
openssl req -newkey rsa:2048 -nodes -keyout key.pem -out csr.pem -config "$CONF"
openssl x509 -req -in csr.pem -CA ca.pem -CAkey ca-key.pem -CAcreateserial \
  -out cert.pem -days "$LEAF_DAYS" -extfile "$CONF" -extensions ext
rm -f csr.pem ca.srl "$CONF" "$CACONF"
chmod 600 key.pem

echo
echo "Checks:"
openssl x509 -in cert.pem -noout -ext subjectAltName 2>/dev/null | sed 's/^/  /'
openssl x509 -in cert.pem -noout -ext extendedKeyUsage 2>/dev/null | sed 's/^/  /'
openssl verify -CAfile ca.pem cert.pem 2>/dev/null | sed 's/^/  /' || true

cat <<EOF

Done. Now:

  1. dotnet run --project Manifest -- --https
  2. On your phone, open the /setup address the server prints and follow it.
     iPhones need a second step in Settings that the page explains.

Prefer not to install anything? Run without --https. Everything works except the
live viewfinder - the Photo button still scans, because uploading a picture does
not need a secure page.
EOF
