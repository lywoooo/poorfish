# poorfish

poorfish is a Unity/C# chess engine I built to study how search and evaluation changes affect gameplay.

It started as a playable chess project, but I expanded it into a small experiment system: the engine can play AI vs AI matches, log search data, and compare different versions under controlled conditions. Early tests showed that side assignment and starting positions could make results misleading, so I added paired starts and fixed match settings before trusting larger runs.

![Chess Screenshot](https://github.com/user-attachments/assets/59b7f82e-e906-4b8c-9ca0-fdbec62a9e7d)

## Play

[Play poorfish on itch.io](https://lywoo.itch.io/poorfish)

---

## What I Built

poorfish supports a full chess game loop in Unity, including legal move generation, castling, en passant, promotion, check, checkmate, stalemate, and game-state tracking.

The engine logic is separate from the Unity board, so positions can be simulated, searched, logged, and tested without relying on scene objects.

Core pieces:

- `BoardState`, a 64-square board model
- legal move generation and rule validation
- minimax search with alpha-beta pruning
- iterative deepening, move ordering, and transposition table caching
- configurable engine profiles for testing search and evaluation changes
- AI vs AI match runner with CSV and PGN output
- per-ply telemetry for moves, evaluations, depth, nodes, cutoffs, time, and FEN

## Experiments

I built a controlled self-play workflow to compare engine versions. The current tests use balanced FEN starting positions, paired color-swapped games, fixed match settings, and consistent logging.

![Controlled self-play results against V1 Baseline](docs/experiments/controlled-results-vs-baseline.svg)

![Version win-rate progression against V1 Baseline](docs/experiments/version-win-rate-progression.svg)

![Version score-rate progression against V1 Baseline](docs/experiments/version-score-rate-progression.svg)

![Paired FEN outcomes against V1 Baseline](docs/experiments/paired-fen-outcomes.svg)

![Search efficiency and depth in controlled self-play](docs/experiments/search-efficiency-depth.svg)

![Game termination breakdown](docs/experiments/termination-breakdown.svg)

### Results Summary

Most of the current evidence comes from 250-game paired-position runs against `V1_Baseline`:

| Test | Result |
| --- | --- |
| `V4_TranspositionTable` vs `V1_Baseline` | 160 wins, 81 draws, 9 losses |
| `V7_Mobility` vs `V1_Baseline` | 139 wins, 82 draws, 29 losses |
| `V8_Endgame` vs `V1_Baseline` | 144 wins, 79 draws, 27 losses |
| `V9_OpeningBook` vs `V1_Baseline` | 146 wins, 75 draws, 29 losses |

`V4_TranspositionTable` had the strongest result against the baseline. Later evaluation-focused versions still won clearly, but their gains were smaller in this test set.

I also tested stronger versions against each other. `V4_TranspositionTable` scored 97 wins, 96 draws, and 57 losses against `V7_Mobility`, which made me question whether extra evaluation terms were helping enough to justify the added complexity.

One early 50-game `V1_Baseline` mirror test ended 12 wins for one side, 21 for the other, and 17 draws. Since both engines were identical, that result pushed me to improve the experiment setup before relying on the larger comparisons.

The `V9_OpeningBook` result is included with context: these tests start from random FEN positions, so the opening book usually does not trigger. I treat that run as another profile comparison, not proof that the book improved play.

## What I Learned

The biggest lesson so far is that deeper search is not automatically better chess. When the evaluation function was weak, searching deeper often led to repetition instead of progress.

This shifted my focus from adding features to testing whether each change actually improved play. The match runner, logs, profiles, and starting positions became just as important as the engine changes themselves.

## Current Focus

poorfish is still being developed. Current goals:

- reduce repetition and safe-loop behavior
- improve evaluation pressure in winning positions
- run cleaner ablation tests
- expand analysis tools for match logs
- refine the WebGL build and player experience

