# poorfish

`poorfish` is a Unity/C# chess project that I have been turning into a playable chess game and small engine-testing environment. It started from an existing Unity chess foundation, but my focus has been building out the rules layer, AI search, position evaluation, self-play logging, and WebGL deployment.

The goal is not just to make pieces move on a board. The project is a way for me to learn how game logic, algorithms, data representation, and user interaction fit together in one system.

<img width="1928" height="1292" alt="Chess_Screenshot" src="https://github.com/user-attachments/assets/59b7f82e-e906-4b8c-9ca0-fdbec62a9e7d" />

## Play it

[Play poorfish on itch.io](https://lywoo.itch.io/poorfish)

## What it does

The current build supports a full chess game loop:

- Board setup and piece placement
- Click and drag interaction
- Legal move highlighting
- Captures and last-move highlighting
- Pawn promotion
- Castling
- En passant
- Check, checkmate, and stalemate detection
- Draw handling for insufficient material, threefold repetition, and the 50-move rule
- Human vs human, human vs AI, and AI vs AI modes
- WebGL builds for browser play

## Technical structure

The project separates the Unity presentation layer from the chess logic. Unity GameObjects handle what the player sees and interacts with, while a separate `BoardState` model represents the actual chess position.

Some of the main systems:

- `BoardState` stores the position as a 64-square array, tracks turn state, castling rights, en passant targets, king positions, halfmove clocks, and last moves.
- `MoveGenerator` creates pseudo-legal moves, then filters them by simulating each move and checking whether the king remains safe.
- `GameManager` connects the board model to the playable Unity scene and handles applied moves, captures, promotion, turn changes, and game-ending conditions.
- `FEN` parsing/export makes board positions easier to save, test, and log using standard chess notation.
- `MoveSelector` handles player interaction, legal move indicators, capture rings, dragging, and promotion choices.

## Chess AI

The AI is search-based rather than random. It uses a minimax-style search with alpha-beta pruning to evaluate future positions and choose moves.

Current engine features include:

- Minimax search with alpha-beta pruning
- Iterative deepening
- Time-limited search
- Transposition table caching
- Move ordering
- Immediate checkmate move detection
- Adaptive search depth in endgames
- Configurable engine profiles through Unity `ScriptableObject` settings
- Opening book support for a small set of common openings

The evaluation function considers more than material:

- Material values
- Piece-square tables
- Optional mobility scoring
- Endgame mate pressure
- King edge pressure
- King distance pressure
- Draw and repetition penalties

## Self-play data

I added AI-vs-AI batch logging so the engine can be tested over multiple games instead of judged only by feel. The logs are still small and experimental, but they already helped me see patterns in how the engine behaves.

Current logged sample:

- 59 AI-vs-AI games
- 23 self-play batches
- 2,884 total plies logged
- Average game length: 48.9 plies
- Median game length: 37 plies
- Longest logged game: 232 plies
- All logged games are Baseline vs Baseline engine matches

Result breakdown:

| Result type | Games | Share |
| --- | ---: | ---: |
| White win | 13 | 22.0% |
| Black win | 10 | 16.9% |
| Draw by insufficient material | 17 | 28.8% |
| Draw by threefold repetition | 19 | 32.2% |

The trend over time is useful even though the dataset is small. Early April 9 runs had more decisive games and much longer average game lengths. Later runs, especially April 20 and April 21, produced more threefold-repetition draws, which suggests the engine can still fall into repeated safe moves in simplified positions. That gives me a concrete next target: improve repetition avoidance, endgame planning, and evaluation pressure instead of only increasing search depth.

## Search statistics

The newer logs include detailed per-ply search statistics for 402 engine moves across 5 games. In that sample, the engine searched:

- 157,316,948 total nodes
- 50,617,653 leaf evaluations
- 75,496,900 transposition table hits
- 22,298,962 alpha-beta cutoffs
- Average completed depth of about 8 plies
- Average search time of about 501.5 ms per move
- Transposition hits equal to about 48.0% of searched nodes
- Alpha-beta cutoffs equal to about 14.2% of searched nodes

These numbers make the project easier to study. Instead of only asking whether the AI won a game, I can look at how much work the engine did, whether caching helped, how often pruning happened, and where the search still struggles.

## What I learned

This project has helped me understand that chess programming is a combination of correctness, algorithms, and measurement.

The hardest parts have been:

- Making move generation legal instead of just plausible
- Handling special rules without breaking normal movement
- Keeping the board model fast enough for repeated search
- Designing an evaluation function that makes the AI play more purposefully
- Logging enough data to find weaknesses in the engine
- Connecting backend chess logic to a playable Unity interface

## Status

`poorfish` is still in progress. The next improvements I want to work on are:

- Better repetition avoidance
- Stronger endgame evaluation
- More varied engine profiles for self-play comparison
- Cleaner analysis scripts for the CSV logs
- More polished browser presentation
- Continuing to improve the itch.io build
