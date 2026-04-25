# poorfish

poorfish is a Unity/C# chess engine I built to study how search and evaluation changes affect gameplay.

It started as a playable chess project, but I expanded it into an experiment system: the engine can play AI vs AI matches, log what it searched, and compare different versions under controlled conditions. The main lesson so far has been that making an engine stronger is not just about adding features. It is also about designing tests that show whether a change actually helped.

![Chess Screenshot](https://github.com/user-attachments/assets/59b7f82e-e906-4b8c-9ca0-fdbec62a9e7d)

## Play

[Play poorfish on itch.io](https://lywoo.itch.io/poorfish)

---

## What I Built

poorfish supports a full chess game loop in Unity, including legal move generation, castling, en passant, promotion, check, checkmate, stalemate, and game-state tracking.

The important design choice is that the engine logic is separate from the Unity board. Positions can be simulated, searched, logged, and tested without depending on scene objects. That made the project useful as both a game and an analysis tool.

Core pieces:

- `BoardState`, a 64-square board model
- legal move generation and rule validation
- minimax search with alpha-beta pruning
- iterative deepening, move ordering, and transposition table caching
- configurable engine profiles for testing search and evaluation changes
- AI vs AI match runner with CSV and PGN output
- per-ply telemetry for moves, evaluations, depth, nodes, cutoffs, time, and FEN

## Experiment Design

My first AI-vs-AI tests were easy to misread. A version could look stronger because of the side it played, the position it started from, or mismatched engine settings. That pushed me to treat the match runner as part of the engineering problem.

The current controlled tests use:

- balanced random FEN starting positions
- paired games where each position is played with colors swapped
- fixed batch settings
- consistent CSV, PGN, and ply-level logs
- engine profiles so techniques can be compared cleanly

![Poorfish controlled self-play experiment overview](docs/experiments/experiment-overview.svg)

![Poorfish experiment method and search telemetry](docs/experiments/experiment-methods-telemetry.svg)

## Results

Most of the current evidence comes from 250-game paired-position runs against `V1_Baseline`:

| Test | Result |
| --- | --- |
| `V4_TranspositionTable` vs `V1_Baseline` | 160 wins, 81 draws, 9 losses |
| `V7_Mobility` vs `V1_Baseline` | 139 wins, 82 draws, 29 losses |
| `V8_Endgame` vs `V1_Baseline` | 144 wins, 79 draws, 27 losses |
| `V9_OpeningBook` vs `V1_Baseline` | 146 wins, 75 draws, 29 losses |

`V4_TranspositionTable` had the strongest result against the baseline. Later evaluation-focused versions still won clearly, but their gains were smaller in this test set.

I also tested stronger versions against each other. `V4_TranspositionTable` scored 97 wins, 96 draws, and 57 losses against `V7_Mobility`, which made me question whether extra evaluation terms were helping enough to justify the added complexity.

One early 50-game `V1_Baseline` mirror test ended 12 wins for one side, 21 for the other, and 17 draws. Since both engines were identical, that result showed me that the experiment setup itself could affect the result.

The `V9_OpeningBook` result is included with context: these tests start from random FEN positions, so the opening book usually does not trigger. I treat that run as another profile comparison, not proof that the book improved play.

## What I Learned

The biggest lesson so far is that deeper search is not automatically better chess. When the evaluation function was weak, searching deeper often led to repetition instead of progress.

That shifted my focus from simply adding features to testing whether each change improved play in a measurable way. The match runner, logs, profiles, and starting positions became just as important as the engine changes themselves.

## Current Focus

poorfish is still being developed. Current goals:

- reduce repetition and safe-loop behavior
- improve evaluation pressure in winning positions
- run cleaner ablation tests
- expand analysis tools for match logs
- refine the WebGL build and player experience
