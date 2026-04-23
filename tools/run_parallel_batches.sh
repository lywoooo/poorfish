#!/usr/bin/env bash

set -euo pipefail

PROJECT_PATH="/Users/leox/Personal/Unity/poorfish"
UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"
EXECUTE_METHOD="PoorfishBatchRunner.Run"

GAMES="${GAMES:-1000}"
DEPTH="${DEPTH:-2}"
MAX_PLIES="${MAX_PLIES:-160}"

TIMESTAMP="$(date +"%Y%m%d_%H%M%S")"
RUN_DIR="$PROJECT_PATH/SelfPlayLogs/parallel_$TIMESTAMP"

mkdir -p "$RUN_DIR"

MATCHUPS=(
  "Assets/Prefabs/V1_Baseline.asset|Assets/Prefabs/V2_ABPruning.asset|v1_vs_v2"
  "Assets/Prefabs/V3_MoveOrdering.asset|Assets/Prefabs/V4_TranspositionTable.asset|v3_vs_v4"
  "Assets/Prefabs/V5_PieceSquareTables.asset|Assets/Prefabs/V6_Development.asset|v5_vs_v6"
  "Assets/Prefabs/V8_Endgame.asset|Assets/Prefabs/V9_OpeningBook.asset|v8_vs_v9"
)

pids=()
names=()

for matchup in "${MATCHUPS[@]}"; do
  IFS="|" read -r white_profile black_profile label <<< "$matchup"

  csv_path="$RUN_DIR/${label}.csv"
  log_path="$RUN_DIR/${label}.log"

  echo "Launching $label"

  "$UNITY_BIN" \
    -batchmode \
    -quit \
    -projectPath "$PROJECT_PATH" \
    -executeMethod "$EXECUTE_METHOD" \
    -white "$white_profile" \
    -black "$black_profile" \
    -games "$GAMES" \
    -depth "$DEPTH" \
    -maxPlies "$MAX_PLIES" \
    -out "$csv_path" \
    -logFile "$log_path" &

  pids+=("$!")
  names+=("$label")
done

exit_code=0

for i in "${!pids[@]}"; do
  pid="${pids[$i]}"
  label="${names[$i]}"

  if wait "$pid"; then
    echo "Finished $label"
  else
    echo "Failed $label" >&2
    exit_code=1
  fi
done

echo "Logs and CSVs written to $RUN_DIR"
exit "$exit_code"
