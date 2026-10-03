#!/usr/bin/env bash

# Central validator for branch names, commit subjects, and pull-request titles.
# Exit codes: 0 = valid, 1 = convention failure, 2 = invalid invocation.

set -u

usage() {
  printf 'Usage: %s <branch|commit|pr> <value>\n' "${0##*/}" >&2
}

fail() {
  printf 'Convention error: %s\n' "$1" >&2
  exit 1
}

is_jira_looking_prefix() {
  [[ "$1" =~ ^[Ss][Aa][Ll][Ii][Nn]($|[^[:alpha:]]) ]]
}

validate_branch() {
  local value="$1"
  local remainder
  local slug
  local -a words

  if [[ "$value" == "dev" || "$value" == "main" ]]; then
    return 0
  fi

  if (( ${#value} > 60 )); then
    fail "branch names must be at most 60 characters"
  fi

  if [[ ! "$value" =~ ^(feature|bugfix|hotfix|chore|refactor|docs|test|spike)/(.+)$ ]]; then
    fail "branch must be '<type>/<description>' with an allowed type"
  fi

  remainder="${BASH_REMATCH[2]}"
  if [[ "$remainder" =~ ^SALIN-[0-9]+-(.+)$ ]]; then
    slug="${BASH_REMATCH[1]}"
  else
    if is_jira_looking_prefix "$remainder"; then
      fail "optional Jira keys must use uppercase 'SALIN-<number>' syntax"
    fi
    slug="$remainder"
  fi

  if [[ ! "$slug" =~ ^[a-z0-9]+(-[a-z0-9]+)+$ ]]; then
    fail "branch descriptions must be lowercase kebab-case"
  fi

  IFS='-' read -r -a words <<< "$slug"
  if (( ${#words[@]} < 2 || ${#words[@]} > 5 )); then
    fail "branch descriptions must contain 2-5 words"
  fi
}

validate_commit() {
  local value="$1"
  local description

  if [[ "$value" =~ ^Merge[[:space:]].+ ]] || [[ "$value" =~ ^Revert[[:space:]]\".+\"$ ]]; then
    return 0
  fi

  if [[ ! "$value" =~ ^(feat|fix|chore|refactor|docs|test|style|perf|ci|revert)(\([a-z0-9-]+\))?:[[:space:]]+([^[:space:]].*)$ ]]; then
    fail "commit subjects must use Conventional Commits with an allowed type"
  fi

  description="${BASH_REMATCH[3]}"
  if [[ "$description" =~ ^SALIN-[0-9]+[[:space:]]+[^[:space:]].*$ ]]; then
    return 0
  fi

  if is_jira_looking_prefix "$description"; then
    fail "optional Jira keys must use 'SALIN-<number> ' before the description"
  fi
}

validate_pr() {
  local value="$1"

  if [[ -z "$value" ]]; then
    fail "pull-request titles cannot be empty"
  fi

  if [[ "$value" =~ ^SALIN-[0-9]+:[[:space:]]+[A-Z0-9].*$ ]]; then
    return 0
  fi

  if is_jira_looking_prefix "$value"; then
    fail "optional Jira keys in pull-request titles must use 'SALIN-<number>: '"
  fi

  if [[ ! "$value" =~ ^[A-Z0-9].*$ ]]; then
    fail "pull-request titles must begin with an uppercase letter or number"
  fi
}

if (( $# != 2 )); then
  usage
  exit 2
fi

case "$1" in
  branch)
    validate_branch "$2"
    ;;
  commit)
    validate_commit "$2"
    ;;
  pr)
    validate_pr "$2"
    ;;
  *)
    usage
    exit 2
    ;;
esac

exit 0
