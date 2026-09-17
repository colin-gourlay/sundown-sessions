# Maintaining security.txt

The website publishes its vulnerability reporting contact at
<https://sundownsessions.co.uk/.well-known/security.txt>, following
[RFC 9116](https://www.rfc-editor.org/rfc/rfc9116.html).
Edit `src/static/.well-known/security.txt`; Hugo copies it unchanged into
`src/public/.well-known/security.txt`.

## Renewal

Review the file at least every six months and before its `Expires` timestamp.
Confirm that the project mailbox still receives reports and is monitored, then
set `Expires` to a UTC RFC 3339 timestamp less than one year ahead, for example
nine months after the review. Commit the update and deploy through a pull request.
Do not renew automatically without checking the reporting route.

Keep `Preferred-Languages: en` and the canonical HTTPS URL. Do not substitute a
personal address or add `Encryption`, `Acknowledgments`, or `Policy` unless a
maintained resource exists.

## Build and deployment verification

Run from the repository root:

```bash
hugo --source src --environment production --minify
cmp src/static/.well-known/security.txt src/public/.well-known/security.txt
```

The Pages workflow compares the source and built files before upload. Its
`include-hidden-files: true` setting is necessary because the upload action
otherwise excludes the `.well-known` directory from the deployment archive.

After merging and deploying, verify the production response:

```bash
curl --fail --silent --show-error --proto '=https' \
  --dump-header /tmp/sundown-security-headers.txt \
  --output /tmp/sundown-security.txt \
  https://sundownsessions.co.uk/.well-known/security.txt
cat /tmp/sundown-security-headers.txt
iconv -f UTF-8 -t UTF-8 /tmp/sundown-security.txt > /dev/null
cmp src/static/.well-known/security.txt /tmp/sundown-security.txt
```

Require HTTP 200 and `Content-Type: text/plain` (with `charset=utf-8` if a charset
is supplied). The UTF-8 decoding and byte comparison must succeed. A redirect,
HTML error page, or stale file does not count as a successful verification.
Record the result in the pull request or issue after deployment; a local build
alone cannot establish production behaviour.
