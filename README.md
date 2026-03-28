# poorfish

A Unity chess project with a real rules layer, a search-based AI opponent, and a playable 3D board.

Still in progress, but there's some thoughtful engineering under the surface.

## What it is

`poorfish` is more than a chess game. It has a proper board-state layer, legal move generation, check detection, end-of-game handling, and a computer opponent that actually evaluates positions before moving. 

## What's working

The current build covers the full main loop of a chess game: board setup, piece selection, move highlighting, captures, checkmate and stalemate detection, and a playable computer opponent.

Highlights:

- **Legal move filtering** checks whether a move would leave your king in check, so it's doing more than basic piece movement.
- **Minimax with alpha-beta pruning**, no random moves. Search is bounded by a time limit and backed by a transposition table to keep things practical.
- **Evaluation beyond material counts** — piece-square tables give the AI a more sensible sense of which positions are actually strong.
- **A WebGL build**, so it's easy to share and play in the browser.

## Running it

1. Open the project in Unity `2022.3.62f1`.
2. Open `Assets/Scenes/Main.unity`.
3. Press Play.

### OR 

- Use the link: [poorfish](https://sgtryan10.github.io/poorfish/)

## Project structure

- `Assets/Scripts` — gameplay code: board state, move generation, evaluation, and AI search.
- `Assets/Scenes/Main.unity` — the main playable scene.
- `docs` — WebGL build for browser hosting.

## Status

The core ideas are in place and some parts are genuinely impressive, especially the rule handling and AI structure. Still, this should be viewed as a strong work in progress.
