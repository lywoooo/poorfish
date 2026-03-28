# poorfish

`poorfish` is a Unity chess project that already does more than just render a board and move pieces around. It has a playable 3D presentation, legal move checking, check and checkmate detection, and a computer opponent that searches ahead instead of picking moves at random.

The project is still actively being developed, so this is best read as a strong in-progress build rather than a finished game.

## What stands out

- It separates the visual board from the game-state logic, which makes the chess rules and AI easier to reason about.
- The move system filters out illegal moves, so pieces cannot make moves that leave their own king in check.
- The AI uses minimax with alpha-beta pruning, iterative deepening, a transposition table, and a think-time limit. In plain terms: it looks ahead efficiently and makes decisions under a real time budget.
- The evaluation system combines material values with piece-square tables, which gives the AI a more believable sense of position than a basic capture-only approach.
- The project handles game-ending states like checkmate and stalemate instead of stopping at simple piece movement.
- There is already a WebGL build in the repo, which makes the project easier to share and test in a browser.

## Why it feels promising

This project has the kind of structure that makes future improvements realistic. The rules engine, board snapshot system, and AI search are already separated into their own scripts, so there is a clear path for improving difficulty, polishing the UI, adding missing chess rules, and tightening up the overall game feel without having to rebuild everything from scratch.

## Current state

Built with Unity `2022.3.62f3`.

The core experience is already there: a playable chess board, piece interaction, move highlighting, captures, and an AI opponent. At the same time, it is still under development, and there is room to keep improving polish, feature completeness, and overall presentation.

## Project layout

- `Assets/Scripts/` contains the gameplay code, including board state, move generation, evaluation, and AI search.
- `Assets/Scenes/Main.unity` is the main scene for the game.
- `docs/` contains a WebGL build suitable for browser hosting.

## Running the project

1. Open the project in Unity `2022.3.62f3`.
2. Load `Assets/Scenes/Main.unity`.
3. Press Play in the Unity editor.

## In development

This project is still being developed. Some parts already show strong technical thinking, especially around chess logic and AI, while other parts are still on the way from solid prototype to fully polished game.
