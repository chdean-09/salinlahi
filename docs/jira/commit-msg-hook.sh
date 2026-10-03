#!/usr/bin/env bash

# Delegates commit-subject validation to the repository's shared validator.

set -u

if (( $# != 1 )) || [[ ! -f "$1" ]]; then
  printf 'commit-msg hook requires a commit-message file\n' >&2
  exit 2
fi

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || {
  printf 'commit-msg hook could not resolve the repository root\n' >&2
  exit 2
}

validator="$repo_root/docs/jira/validate-git-conventions.sh"
if [[ ! -x "$validator" ]]; then
  printf 'commit-msg validator is missing or not executable: %s\n' "$validator" >&2
  exit 2
fi

subject="$(sed -n '1p' "$1")"
exec "$validator" commit "$subject"
