# poorfish

A Unity chess project with a real rules layer, a search-based AI opponent, and a playable 3D board.

This is still an in-progress project, but it already shows some thoughtful engineering under the surface.

## Highlights

- Playable 3D chess board built in Unity, with piece selection, move highlighting, captures, and turn handling.
- Legal move generation checks whether a move would leave your king in check, so the game does more than basic piece movement.
- The AI searches ahead using minimax with alpha-beta pruning instead of choosing moves randomly.
- Search is bounded by a time limit and uses a transposition table, which helps it stay practical while still looking ahead.
- Position evaluation uses both material values and piece-square tables, giving the AI a more sensible idea of strong and weak positions.
- The project already includes a WebGL build, which makes it easier to share in the browser.

## Overview

`poorfish` is not just a visual chess prototype. The project has a separate board-state layer, move generation, check detection, end-of-game handling, and a computer player that evaluates positions before moving.

That separation matters because it makes the project easier to grow. The logic for rules, state snapshots, and AI search is already organized into dedicated scripts, which gives the project a solid base for future polish and feature work.

## What is working now

The current build already supports the main loop of a chess game:

- Board setup and piece placement
- Piece selection and move highlighting
- Legal move filtering
- Captures
- Checkmate and stalemate detection
- A computer-controlled opponent

Built with Unity `2022.3.62f3`.

## Running the project

1. Open the project in Unity `2022.3.62f3`.
2. Open [Assets/Scenes/Main.unity](/Users/leox/Personal/Unity/poorfish/Assets/Scenes/Main.unity).
3. Press Play in the Unity editor.

## Project structure

- [Assets/Scripts](/Users/leox/Personal/Unity/poorfish/Assets/Scripts) contains the gameplay code, including board state, move generation, evaluation, and AI search.
- [Assets/Scenes/Main.unity](/Users/leox/Personal/Unity/poorfish/Assets/Scenes/Main.unity) is the main playable scene.
- [docs](/Users/leox/Personal/Unity/poorfish/docs) contains a WebGL build for browser hosting.

## Development status

This project is still being developed. The core ideas are already in place and some parts are genuinely impressive, especially the rule handling and AI structure, but it should still be viewed as a strong work in progress rather than a finished chess game.
