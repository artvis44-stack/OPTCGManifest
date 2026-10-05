#!/bin/sh
# SeaweedFS as a self-hosted S3 store for card art, with the one key pair the app
# uses taken from the environment rather than a file in the repository.
set -e
cat > /tmp/s3.json <<JSON
{"identities":[{"name":"manifest","credentials":[{"accessKey":"$S3_ACCESS_KEY","secretKey":"$S3_SECRET_KEY"}],"actions":["Admin","Read","Write","List","Tagging"]}]}
JSON
exec weed server -dir=/data -s3 -s3.port=8333 -s3.config=/tmp/s3.json -volume.max=0
