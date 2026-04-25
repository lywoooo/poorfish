# poorfish

`poorfish` is my Unity/C# chess engine project. It started as a playable chess game, but I have been turning it into a way to test how search algorithms and evaluation functions affect an engine's decisions.

The project is still in progress. A lot of the work has been about making the engine stronger, but just as much has been about learning how to measure whether a change actually helped.

![Chess Screenshot](https://github.com/user-attachments/assets/59b7f82e-e906-4b8c-9ca0-fdbec62a9e7d)

## Play

[Play poorfish on itch.io](https://lywoo.itch.io/poorfish)

---

## What I Built

`poorfish` supports a full chess game loop in Unity, including legal move generation, castling, en passant, promotion, check detection, checkmate, stalemate, and game-state tracking.

The engine is not tied to the Unity board. I separated the chess logic from the rendering layer so positions can be simulated, searched, logged, and compared without relying on scene objects.

Core pieces include:

- `BoardState`, a 64-square board model used by the engine
- legal move generation and rule validation over that model
- a search pipeline that can run independently of Unity visuals
- engine profiles for comparing different search and evaluation ideas
- AI-vs-AI match running with CSV and PGN output
- per-ply telemetry for analyzing how the engine searched each position

That separation made the project useful for more than playing games. It let me run matches, compare versions, and inspect what the engine was doing move by move.

## Chess Engine

The engine uses a traditional search-based approach:

- minimax with alpha-beta pruning
- iterative deepening with time limits
- transposition table caching
- move ordering
- adaptive endgame depth
- configurable evaluator weights

The evaluator includes more than material count. It can consider piece-square tables, mobility, king pressure, endgame behavior, and profile-specific weights. This lets me compare versions of the engine by changing one idea at a time instead of treating the AI as a black box.

I also log the engine's search behavior:

- selected moves and evaluations
- nodes searched
- alpha-beta cutoffs
- transposition table hits
- depth reached
- time spent per move
- FEN context before each move

Those logs helped me debug the engine, but they also made it harder to fool myself. A feature could sound useful without actually improving the engine's play, so I needed data from real games.

## Experiments

I built a self-play experiment workflow to compare engine versions across many games. The current controlled tests use balanced random FEN positions, color-swapped pairs, fixed batch settings, and per-ply search telemetry.

This matters because my early AI-vs-AI tests were easy to misread. A version could look stronger because it played White more often, started from easier positions, or used different search settings. I added guardrails and paired starts so the results were more fair.

![Controlled self-play results against V1 Baseline](docs/experiments/controlled-results-vs-baseline.svg)

![Version win-rate progression against V1 Baseline](docs/experiments/version-win-rate-progression.svg)

![Version score-rate progression against V1 Baseline](docs/experiments/version-score-rate-progression.svg)

![Paired FEN outcomes against V1 Baseline](docs/experiments/paired-fen-outcomes.svg)

![Search efficiency and depth in controlled self-play](docs/experiments/search-efficiency-depth.svg)

![Game termination breakdown](docs/experiments/termination-breakdown.svg)

In one 250-game controlled comparison, the search-focused `V4_TranspositionTable` profile scored 160 wins, 81 draws, and 9 losses against `V1_Baseline`. Later evaluation-heavy versions also beat the baseline, but not as strongly in that test set.

The `V9_OpeningBook` profile is labeled carefully because these random-FEN experiments do not really test the opening book. Since the games start from generated positions instead of standard openings, the book did not trigger. I kept that distinction because I want the results to show what was actually tested.

## What I Learned

The biggest lesson so far is that deeper search is not automatically better chess.

When the evaluation function was weak, searching farther sometimes made the engine repeat safe moves instead of making progress. That shifted my focus from simply adding more features to studying the relationship between search, evaluation, and incentives.

I also learned that experiments need engineering too. The match runner, logging format, profile system, and starting positions all affect whether the results mean anything.

At first I mostly asked, "Can I make the bot stronger?" Now I spend more time asking what changed, how I can measure it, and whether the test is fair.

## Current Focus

`poorfish` is still being developed. My next goals are:

- reduce repetition and safe-loop behavior
- improve evaluation pressure in winning positions
- run cleaner ablation tests for individual techniques
- expand the analysis tools around match logs
- continue refining the WebGL build and player experience

## Why I Built It

Chess engines are interesting to me because the rules are exact, but the decisions are not simple. Small changes in search or evaluation can change the engine's style in ways that are hard to predict.

`poorfish` gave me a project where I could combine programming, chess, and experimentation. I am still improving it, but it has already taught me a lot about building systems that can be tested instead of just demoed.
