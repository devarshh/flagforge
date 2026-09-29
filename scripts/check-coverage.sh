#!/usr/bin/env bash
# Summarizes Cobertura coverage for one assembly and fails when line coverage is below a minimum.
#
# Usage: scripts/check-coverage.sh <cobertura-report-glob> <assembly-name> <minimum-line-percent> [output-dir]
# Example: scripts/check-coverage.sh 'TestResults/**/*.cobertura.xml' FlagForge.Evaluation 95
#
# Writes Summary.txt and SummaryGithub.md (for $GITHUB_STEP_SUMMARY) to the output directory.
set -euo pipefail

if [[ $# -lt 3 ]]; then
  echo "usage: $0 <cobertura-report-glob> <assembly-name> <minimum-line-percent> [output-dir]" >&2
  exit 2
fi

reports="$1"
assembly="$2"
minimum="$3"
output_dir="${4:-TestResults/coverage-report}"

dotnet tool run reportgenerator \
  "-reports:${reports}" \
  "-targetdir:${output_dir}" \
  "-reporttypes:TextSummary;MarkdownSummaryGithub" \
  "-assemblyfilters:+${assembly}" \
  "-filefilters:-*.g.cs" \
  "-verbosity:Warning"

line_coverage="$(grep -E '^[[:space:]]*Line coverage:' "${output_dir}/Summary.txt" | head -n 1 | sed -E 's/.*:[[:space:]]*([0-9.]+)%.*/\1/')"
if [[ -z "${line_coverage}" ]]; then
  echo "Could not read line coverage for ${assembly} from ${output_dir}/Summary.txt" >&2
  exit 1
fi

echo "${assembly} line coverage: ${line_coverage}% (minimum ${minimum}%)"
if ! awk -v actual="${line_coverage}" -v required="${minimum}" 'BEGIN { exit !(actual + 0 >= required + 0) }'; then
  echo "Line coverage for ${assembly} is ${line_coverage}%, below the required ${minimum}%." >&2
  exit 1
fi
