#!/usr/bin/env bash
# Fails when a committed asset under Assets/ has no .meta file, or a .meta file has no asset.
# Unity needs both halves: a missing .meta breaks references on other machines,
# and an orphaned one points at a file nobody committed.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

git -c core.quotepath=off ls-files -- Assets | awk '
  # Unity ignores names that start with "." or end with "~" or ".tmp".
  function hidden(name) { return name ~ /^\./ || name ~ /~$/ || name ~ /\.tmp$/ }
  {
    n = split($0, parts, "/")
    for (i = 2; i < n; i++) if (hidden(parts[i])) next

    # Every folder below Assets/ needs its own .meta file.
    dir = parts[1]
    for (i = 2; i < n; i++) { dir = dir "/" parts[i]; assets[dir] = 1 }

    if (hidden(parts[n])) next
    if ($0 ~ /\.meta$/) metas[substr($0, 1, length($0) - 5)] = 1
    else assets[$0] = 1
  }
  END {
    for (a in assets) if (!(a in metas)) { print "Missing .meta file for: " a; bad = 1 }
    for (m in metas) if (!(m in assets)) { print "Orphaned .meta file: " m ".meta"; bad = 1 }
    if (!bad) print "All assets have matching .meta files."
    exit bad
  }
'
