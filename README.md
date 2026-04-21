# poorfish

`poorfish` is a Unity/C# chess engine and experimentation system focused on understanding how search algorithms and evaluation functions affect decision-making.

It started from a Unity chess foundation, but I’ve been extending it into a system I can analyze rather than just play.

![Chess Screenshot](https://github.com/user-attachments/assets/59b7f82e-e906-4b8c-9ca0-fdbec62a9e7d)

## Play it

[Play poorfish on itch.io](https://lywoo.itch.io/poorfish)

---

## Overview

The project supports a full chess game loop with legal move generation, special rules (castling, en passant, promotion), and complete game-state handling.

More importantly, it is structured to allow fast simulation and experimentation on chess positions rather than relying on Unity scene objects.

---

## Architecture

The system separates game logic from rendering:

- `BoardState` represents the position as a 64-square array
- Move generation and rule validation operate entirely on this model
- Search runs independently of Unity objects
- Unity handles visualization and interaction

This separation allows the engine to simulate positions quickly and run AI search and experiments without scene overhead.

---

## Chess AI

The engine uses a search-based approach:

- Minimax with alpha-beta pruning
- Iterative deepening and time-limited search
- Transposition table caching
- Move ordering and adaptive endgame depth

The evaluation function goes beyond material:

- Piece-square tables
- Mobility and positional heuristics
- King pressure and endgame features
- Configurable weights through engine profiles

To better understand behavior, I added instrumentation:

- nodes explored  
- alpha-beta cutoffs  
- transposition hits  
- search depth and time  

This lets me measure how the engine works instead of treating it as a black box.

---

## Experimentation

I built an AI-vs-AI batch system with CSV logging so I could compare engine configurations across full games.

Logged data includes:

- FEN positions  
- evaluation scores  
- search depth  
- nodes explored  
- pruning cutoffs  
- transposition hits  

Current sample:

- 59 games (baseline vs baseline)  
- 2,884 plies logged  
- average game length: 48.9 plies  

Results show a high rate of repetition-based draws in simplified positions.

This suggests the engine prioritizes safe repetition over meaningful progress, giving me a concrete next direction: improving evaluation pressure instead of only increasing search depth.

---

## Key Insight

One of the main insights from this project is that deeper search does not necessarily lead to better play.

In many cases, increasing depth without improving evaluation caused the engine to repeat moves or fail to make progress.

This shifted my focus from adding features to understanding how search and evaluation interact, and treating the engine as something to measure and test rather than something to complete.

---

## Status

`poorfish` is still in progress. Current focus areas:

- reducing repetition in endgames  
- strengthening evaluation heuristics  
- comparing engine profiles through self-play  
- improving analysis tooling for logged data  
- refining the WebGL build  