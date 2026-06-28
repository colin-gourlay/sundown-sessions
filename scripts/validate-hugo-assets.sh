#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
content_root="${1:-$repo_root/src/content}"
static_root="${2:-$repo_root/src/static}"
report_file="${3:-build/missing-assets.txt}"

mkdir -p "$(dirname "$report_file")"

if [ ! -d "$content_root" ]; then
  echo "Content directory not found: $content_root" >&2
  exit 2
fi

if [ ! -d "$static_root" ]; then
  echo "Static directory not found: $static_root" >&2
  exit 2
fi

tmp_refs="$(mktemp)"
trap 'rm -f "$tmp_refs"' EXIT

extract_front_matter_refs() {
  local file="$1"
  awk '
    NR == 1 && $0 !~ /^---[[:space:]]*$/ { exit }
    /^---[[:space:]]*$/ {
      if (in_fm == 0) {
        in_fm = 1
        next
      }
      if (in_fm == 1) {
        exit
      }
    }
    in_fm == 1 {
      if ($0 ~ /^[[:space:]]*images:[[:space:]]*$/) {
        in_images = 1
        next
      }

      if (in_images == 1) {
        if ($0 ~ /^[[:space:]]*-[[:space:]]+/) {
          value = $0
          sub(/^[[:space:]]*-[[:space:]]+/, "", value)
          gsub(/^["\047]|["\047]$/, "", value)
          print value
          next
        }

        if ($0 ~ /^[[:space:]]*$/) {
          next
        }

        in_images = 0
      }

      if ($0 ~ /^[[:space:]]*(featured_image|image|hero_image|thumbnail|cover_image):[[:space:]]+/) {
        value = $0
        sub(/^[[:space:]]*(featured_image|image|hero_image|thumbnail|cover_image):[[:space:]]+/, "", value)
        gsub(/^["\047]|["\047]$/, "", value)
        print value
      }
    }
  ' "$file"
}

while IFS= read -r -d '' file; do
  while IFS= read -r ref; do
    [ -z "$ref" ] && continue
    printf '%s\t%s\n' "$file" "$ref" >> "$tmp_refs"
  done < <(extract_front_matter_refs "$file")
done < <(find "$content_root" -type f -name '*.md' -print0)

missing_count=0

{
  echo "Missing asset references"
  echo "content_root=$content_root"
  echo "static_root=$static_root"
  echo
} > "$report_file"

while IFS=$'\t' read -r source_file raw_ref; do
  ref="${raw_ref%%#*}"
  ref="${ref%%\?*}"

  if [ -z "$ref" ]; then
    continue
  fi

  case "$ref" in
    http://*|https://*|data:*|mailto:*|tel:*|{{*|*}}*)
      continue
      ;;
  esac

  candidate_1=""
  candidate_2=""

  if [[ "$ref" == /* ]]; then
    candidate_1="$static_root$ref"
  else
    candidate_1="$(dirname "$source_file")/$ref"
    candidate_2="$static_root/$ref"
  fi

  if [ -f "$candidate_1" ]; then
    continue
  fi

  if [ -n "$candidate_2" ] && [ -f "$candidate_2" ]; then
    continue
  fi

  missing_count=$((missing_count + 1))
  {
    echo "source: $source_file"
    echo "reference: $raw_ref"
    if [ -n "$candidate_2" ]; then
      echo "checked: $candidate_1"
      echo "checked: $candidate_2"
    else
      echo "checked: $candidate_1"
    fi
    echo
  } >> "$report_file"
done < <(sort -u "$tmp_refs")

if [ "$missing_count" -eq 0 ]; then
  echo "No missing asset references found." >> "$report_file"
fi

cat "$report_file"

if [ "$missing_count" -gt 0 ]; then
  echo "Found $missing_count missing asset reference(s)." >&2
  exit 1
fi

exit 0
