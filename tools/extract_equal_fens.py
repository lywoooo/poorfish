#!/usr/bin/env python3
"""
Extract relatively equal chess positions from a large Lichess PGN dump.

Requires:
    python3 -m pip install chess

Example:
    python3 tools/extract_equal_fens.py ~/Downloads/lichess_games.pgn.zst \
        --output Assets/Resources/equal_positions.fens \
        --target 200 \
        --min-ply 12 \
        --max-ply 80

Notes:
    - Plain .pgn, .pgn.gz, .pgn.bz2, and .pgn.zst inputs are supported.
    - .zst support requires the `zstd` command line tool.
    - If Lichess eval comments like [%eval 0.23] are present, the script
      filters by that score. Otherwise it uses material balance as a proxy.
"""

from __future__ import annotations

import argparse
import bz2
import gzip
import io
import random
import re
import subprocess
import sys
from collections import Counter
from pathlib import Path
from typing import Iterable, Optional, TextIO

try:
    import chess
    import chess.pgn
except ImportError:
    print(
        "Missing dependency: python-chess.\n"
        "Install it with: python3 -m pip install chess",
        file=sys.stderr,
    )
    raise SystemExit(2)


EVAL_RE = re.compile(r"\[%eval\s+([+#-]?(?:\d+(?:\.\d+)?|#-?\d+))\]")

PIECE_VALUES = {
    chess.PAWN: 100,
    chess.KNIGHT: 320,
    chess.BISHOP: 330,
    chess.ROOK: 500,
    chess.QUEEN: 900,
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract balanced FEN positions from a large Lichess PGN dump."
    )
    parser.add_argument("pgn_path", type=Path, help="Input .pgn, .pgn.gz, .pgn.bz2, or .pgn.zst file.")
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("SelfPlayLogs/equal_positions.fens"),
        help="Output text file, one FEN per line.",
    )
    parser.add_argument("--target", type=int, default=200, help="Number of FENs to write.")
    parser.add_argument("--seed", type=int, default=1, help="Random seed for repeatable sampling.")
    parser.add_argument("--min-ply", type=int, default=12, help="Earliest ply to sample.")
    parser.add_argument("--max-ply", type=int, default=80, help="Latest ply to sample.")
    parser.add_argument(
        "--sample-every",
        type=int,
        default=4,
        help="Only consider every Nth ply in the sample range.",
    )
    parser.add_argument(
        "--max-material-diff",
        type=int,
        default=150,
        help="Maximum material difference in centipawns when no eval is available.",
    )
    parser.add_argument(
        "--max-eval",
        type=float,
        default=0.60,
        help="Maximum absolute embedded eval, in pawns, when eval comments exist.",
    )
    parser.add_argument(
        "--min-rating",
        type=int,
        default=1600,
        help="Skip games where either player rating is below this value.",
    )
    parser.add_argument(
        "--allowed-results",
        default="1-0,0-1,1/2-1/2",
        help="Comma-separated PGN results to accept.",
    )
    parser.add_argument(
        "--max-games",
        type=int,
        default=0,
        help="Stop after this many games. 0 means no limit.",
    )
    parser.add_argument(
        "--no-checks",
        action="store_true",
        help="Skip positions where side to move is in check.",
    )
    parser.add_argument(
        "--csv",
        type=Path,
        default=None,
        help="Optional metadata CSV output with source game and score information.",
    )
    parser.add_argument(
        "--debug-input",
        action="store_true",
        help="Print the first decompressed text chunk and exit.",
    )
    return parser.parse_args()


def open_pgn(path: Path) -> tuple[TextIO, Optional[subprocess.Popen[bytes]]]:
    compression = detect_compression(path)
    if compression == "gzip":
        return gzip.open(path, mode="rt", encoding="utf-8", errors="replace"), None

    if compression == "bzip2":
        return bz2.open(path, mode="rt", encoding="utf-8", errors="replace"), None

    if compression == "zstd":
        process = subprocess.Popen(
            ["zstd", "-dc", str(path)],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        if process.stdout is None:
            raise RuntimeError("Failed to open zstd stdout.")
        return io.TextIOWrapper(process.stdout, encoding="utf-8", errors="replace"), process

    return open(path, mode="rt", encoding="utf-8", errors="replace"), None


def detect_compression(path: Path) -> str:
    with open(path, "rb") as raw_file:
        header = raw_file.read(4)

    if header.startswith(b"\x1f\x8b"):
        return "gzip"

    if header.startswith(b"BZh"):
        return "bzip2"

    if header == b"\x28\xb5\x2f\xfd":
        return "zstd"

    suffixes = "".join(path.suffixes).lower()
    if suffixes.endswith(".gz"):
        return "gzip"

    if suffixes.endswith(".bz2"):
        return "bzip2"

    if suffixes.endswith(".zst"):
        return "zstd"

    return "plain"


def debug_input(path: Path) -> int:
    print(f"path={path}")
    print(f"exists={path.exists()}")
    if path.exists():
        print(f"bytes={path.stat().st_size}")
        print(f"compression={detect_compression(path)}")

    pgn_file, process = open_pgn(path)
    try:
        sample = pgn_file.read(2000)
        print("first_decompressed_chars:")
        print(sample)
        if "[Event " not in sample and "1." not in sample:
            print(
                "warning: sample does not look like PGN. Check that this is a PGN file, "
                "not a Lichess JSON/NDJSON or database index file.",
                file=sys.stderr,
            )
        return 0
    finally:
        pgn_file.close()
        if process is not None:
            process.wait()


def material_score(board: chess.Board) -> int:
    score = 0
    for piece_type, value in PIECE_VALUES.items():
        score += len(board.pieces(piece_type, chess.WHITE)) * value
        score -= len(board.pieces(piece_type, chess.BLACK)) * value
    return score


def embedded_eval(comment: str) -> Optional[float]:
    match = EVAL_RE.search(comment or "")
    if match is None:
        return None

    token = match.group(1)
    if token.startswith("#"):
        return None

    try:
        return float(token)
    except ValueError:
        return None


def rating(headers: chess.pgn.Headers, key: str) -> int:
    try:
        return int(headers.get(key, "0"))
    except ValueError:
        return 0


def should_consider_game(
    game: chess.pgn.Game,
    allowed_results: set[str],
    min_rating: int,
) -> bool:
    headers = game.headers
    if headers.get("Result", "*") not in allowed_results:
        return False

    if rating(headers, "WhiteElo") < min_rating:
        return False

    if rating(headers, "BlackElo") < min_rating:
        return False

    return True


def phase_key(board: chess.Board) -> str:
    pieces = len(board.piece_map())
    if pieces >= 24:
        return "opening_middlegame"
    if pieces >= 14:
        return "middlegame_endgame"
    return "endgame"


def position_passes_filters(
    board: chess.Board,
    eval_score: Optional[float],
    args: argparse.Namespace,
) -> bool:
    if args.no_checks and board.is_check():
        return False

    if board.is_game_over(claim_draw=True):
        return False

    material_diff = abs(material_score(board))
    if eval_score is not None:
        return abs(eval_score) <= args.max_eval and material_diff <= args.max_material_diff * 2

    return material_diff <= args.max_material_diff


def normalized_fen(board: chess.Board) -> str:
    # Keep all standard FEN fields so Unity can load side-to-move, castling,
    # en-passant, halfmove, and fullmove state.
    return board.fen()


def iter_candidate_positions(
    game: chess.pgn.Game,
    args: argparse.Namespace,
) -> Iterable[tuple[str, Optional[float], int, int, str]]:
    board = game.board()
    game_id = game.headers.get("Site", game.headers.get("UTCDate", "unknown"))

    for ply, node in enumerate(game.mainline(), start=1):
        board.push(node.move)
        if ply < args.min_ply or ply > args.max_ply:
            continue

        if args.sample_every > 1 and (ply - args.min_ply) % args.sample_every != 0:
            continue

        eval_score = embedded_eval(node.comment)
        if not position_passes_filters(board, eval_score, args):
            continue

        yield normalized_fen(board), eval_score, ply, material_score(board), game_id


def add_reservoir_item(
    reservoir: list[tuple[str, Optional[float], int, int, str]],
    item: tuple[str, Optional[float], int, int, str],
    seen: int,
    target: int,
    rng: random.Random,
) -> None:
    if len(reservoir) < target:
        reservoir.append(item)
        return

    replacement_index = rng.randrange(seen)
    if replacement_index < target:
        reservoir[replacement_index] = item


def write_outputs(
    positions: list[tuple[str, Optional[float], int, int, str]],
    output_path: Path,
    csv_path: Optional[Path],
) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, mode="w", encoding="utf-8") as fen_file:
        for fen, _, _, _, _ in positions:
            fen_file.write(fen)
            fen_file.write("\n")

    if csv_path is None:
        return

    csv_path.parent.mkdir(parents=True, exist_ok=True)
    with open(csv_path, mode="w", encoding="utf-8") as csv_file:
        csv_file.write("fen,eval,ply,material_cp,source_game\n")
        for fen, eval_score, ply, material_cp, game_id in positions:
            eval_text = "" if eval_score is None else f"{eval_score:.3f}"
            csv_file.write(
                csv_escape(fen)
                + ","
                + eval_text
                + ","
                + str(ply)
                + ","
                + str(material_cp)
                + ","
                + csv_escape(game_id)
                + "\n"
            )


def csv_escape(value: str) -> str:
    if any(char in value for char in [",", "\"", "\n"]):
        return "\"" + value.replace("\"", "\"\"") + "\""
    return value


def main() -> int:
    args = parse_args()
    if not args.pgn_path.exists():
        print(f"Input file does not exist: {args.pgn_path}", file=sys.stderr)
        return 1

    if args.debug_input:
        return debug_input(args.pgn_path)

    allowed_results = {item.strip() for item in args.allowed_results.split(",") if item.strip()}
    rng = random.Random(args.seed)
    positions: list[tuple[str, Optional[float], int, int, str]] = []
    seen_candidates = 0
    games_read = 0
    games_used = 0
    phase_counts: Counter[str] = Counter()
    seen_fens: set[str] = set()

    pgn_file, process = open_pgn(args.pgn_path)
    try:
        while True:
            game = chess.pgn.read_game(pgn_file)
            if game is None:
                break

            games_read += 1
            if args.max_games > 0 and games_read > args.max_games:
                break

            if not should_consider_game(game, allowed_results, args.min_rating):
                continue

            game_contributed = False
            for item in iter_candidate_positions(game, args):
                fen = item[0]
                placement_key = " ".join(fen.split(" ")[:4])
                if placement_key in seen_fens:
                    continue

                seen_fens.add(placement_key)
                seen_candidates += 1
                phase_counts[phase_key(chess.Board(fen))] += 1
                add_reservoir_item(positions, item, seen_candidates, args.target, rng)
                game_contributed = True

            if game_contributed:
                games_used += 1

            if games_read % 1000 == 0:
                print(
                    f"games={games_read} used={games_used} "
                    f"candidates={seen_candidates} kept={len(positions)}",
                    file=sys.stderr,
                )

        positions.sort(key=lambda item: (item[2], item[0]))
        write_outputs(positions, args.output, args.csv)

        print(f"Wrote {len(positions)} FENs to {args.output}")
        print(f"Read {games_read} games, used {games_used}, saw {seen_candidates} candidates")
        print("Phase counts: " + ", ".join(f"{key}={value}" for key, value in phase_counts.items()))
        return 0
    finally:
        pgn_file.close()
        if process is not None:
            process.wait()


if __name__ == "__main__":
    raise SystemExit(main())
