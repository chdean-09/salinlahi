#!/bin/bash
# Git commit message hook for Jira integration
# Validates that commit messages include a SALIN-XX ticket key
#
# To install:
#   cp docs/jira/commit-msg-hook.sh .git/hooks/commit-msg
#   chmod +x .git/hooks/commit-msg

commit_msg_file=$1
commit_msg=$(head -n1 "$commit_msg_file")

# Pattern matches: feat(scope): SALIN-11 description
# Or: SALIN-11: description
jira_pattern="^(([a-z]+(\([a-z-]+\))?: )?SALIN-[0-9]+ .+|SALIN-[0-9]+: .+)"

if ! echo "$commit_msg" | grep -qiE "$jira_pattern"; then
    echo ""
    echo "❌ Commit message rejected!"
    echo ""
    echo "Commit message must include a Jira ticket key (SALIN-XX)."
    echo ""
    echo "Valid formats:"
    echo "  feat(scene-loader): SALIN-11 add async loading"
    echo "  fix(ui): SALIN-102 resolve null reference"
    echo "  SALIN-11: Implement scene loader"
    echo ""
    echo "Your message: $commit_msg"
    echo ""
    echo "See docs/jira/TEAM-HANDBOOK.md for details."
    echo ""
    exit 1
fi

echo "✅ Commit message includes Jira ticket key"
exit 0
