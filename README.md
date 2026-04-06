# poorfish

A Unity chess project with a real rules layer, a search-based AI opponent, and a playable 3D board.

Still in progress. Detached from original [poorfish repo](https://github.com/Sgtryan10/poorfish.git).

<img width="1928" height="1292" alt="Chess_Screenshot" src="https://github.com/user-attachments/assets/59b7f82e-e906-4b8c-9ca0-fdbec62a9e7d" />

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

- Use the link: [poorfish](https://sgtryan10.github.io/poorfish/) (Will convert to itch.io later)

## Status

This project is still a work in progress! More updates coming soon. 
