# log-extractor enrich subagent task

Resolve your SKILL_DIR via Step 0 of log-extractor first (the standard plugin-cache → dev-checkout fallback). Do NOT inline a `SKILL_DIR=...` assignment-prefix on the same command line as the script call — bash expands the path before the prefix takes effect, leaving you with `/scripts/enrich-payload` and exit 127.

Then execute exactly this command (paths and CSV values are pre-escaped; do not re-quote or re-escape):

```bash
"${SKILL_DIR}/scripts/enrich-payload" \
  --input /Users/li-j/project/godot-project/godot-ai-match-game/.buglyn/bug-20260507-171249-9_7443ts/jira_payload.json \
  --skip-enriched \
  --auto-recover \
  --business-ns Ballista,ActionEngine \
  --rca-keywords Quest,Cutin
```

The script enriches `issues[].log_refs[].url` placeholders in the jira_payload file in place. `--skip-enriched` short-circuits prior-pass entries; `--auto-recover` triggers an OAuth re-login flow on auth failures (timeout 600s).

Output only the script's final JSON line on stdout. Do not paraphrase, summarise, or wrap it.
