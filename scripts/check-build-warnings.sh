#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 2 ] || [ "$#" -gt 3 ]; then
  echo "Usage: $0 <baseline-file> <warnings-file> [report-file]" >&2
  exit 2
fi

baseline_file="$1"
warnings_file="$2"
report_file="${3:-build/warning-delta.txt}"
mode="${CHECK_MODE:-report}"

mkdir -p "$(dirname "$report_file")"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

normalise() {
  local src="$1"
  if [ ! -f "$src" ]; then
    touch "$2"
    return
  fi

  grep -v '^[[:space:]]*$' "$src" \
    | grep -v '^[[:space:]]*#' \
    | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' \
    | sort -u > "$2" || true
}

normalise "$baseline_file" "$tmp_dir/baseline.txt"
normalise "$warnings_file" "$tmp_dir/current.txt"

comm -13 "$tmp_dir/baseline.txt" "$tmp_dir/current.txt" > "$tmp_dir/new.txt"
comm -23 "$tmp_dir/baseline.txt" "$tmp_dir/current.txt" > "$tmp_dir/resolved.txt"

new_count="$(wc -l < "$tmp_dir/new.txt" | tr -d ' ')"
resolved_count="$(wc -l < "$tmp_dir/resolved.txt" | tr -d ' ')"

{
  echo "Mode: $mode"
  echo "Baseline file: $baseline_file"
  echo "Warnings file: $warnings_file"
  echo "New warnings: $new_count"
  echo "Resolved warnings: $resolved_count"
  echo

  if [ "$new_count" -gt 0 ]; then
    echo "New warnings:"
    cat "$tmp_dir/new.txt"
    echo
  fi

  if [ "$resolved_count" -gt 0 ]; then
    echo "Resolved warnings:"
    cat "$tmp_dir/resolved.txt"
    echo
  fi
} > "$report_file"

cat "$report_file"

if [ "$mode" = "enforce" ] && [ "$new_count" -gt 0 ]; then
  echo "Found new Hugo warnings compared to baseline." >&2
  exit 1
fi

exit 0
