PoorFish Chess AI Development Log
Leo Xia

Preamble
This is a personal continuation of the PoorFish Chess AI Project built by Ryan Xu, Dorian Choy, Leo Xia, Alvin Zhang, and William Zhang. The project began with a baseline implementation using material and piece-square table evaluation combined with a minimax search enhanced by alpha-beta pruning.	

I plan to expand this into an experimental framework for testing different search algorithms and evaluation heuristics. The focus is on iterative improvement through controlled experiments, benchmarking, and analysis of decision-making behavior under varying configurations. Key areas of exploration include move ordering, evaluation tuning, and the impact of different search strategies on both performance and play style.
Main Goal: refactoring all scripts to work with the new bit flag pieces and 2d sprite configuration. Be ready to test V1 and V2, possibly V3 if time permits.
V1: material only
V2: material + PST
V3: V2 + move ordering
V4: V3 + mobility
V5: V4 + king safety
V6: V5 + quiescence
Add minimax and alpha-beta

Then benchmark:
node count
time per move
depth reached
best move accuracy
win rate vs older versions

Script Goals: 
GameManager: controls the flow of the match; handles game start, turn resolution, game settings, game over manager, etc.
Board: handles game state: piece placement, applying or undoing moves, castling rights, en passant, counting plys/moves, king positions, checking occupied squares, etc
BoardState: log important changes like king positions, castling rights, en passant squares, captured pieces etc. 
Main Idea: Separate chess logic, search, experiment, and Unity visuals.

Architecture
Presentation Layer
BoardUI.cs: draws the board and updates visual state
SquareUI.cs:  epresents a single board square visually
PieceSpriteLibrary.cs: maps piece codes to sprites
MoveListUI.cs: displays move history
EvalBarUI.cs: displays evaluation visually
GameStatusUI.cs: shows turn, checkmate, stalemate, and other game states
Game Layer
GameManager.cs: initializes the game, connects systems, and manages game start/end settings
GameController.cs: controls turn resolution, applies moves, updates UI, detects game end, and maintains move history
PlayerController.cs: abstract base class for human and AI players
HumanPlayer.cs
AIPlayer.cs 
Core Layer
Board.cs: stores the current position and handles move application/undo, castling rights, en passant, move counters, and king positions
Move.cs: compact representation of a move and its flags
BoardState.cs: stores only the data needed to undo a move correctly 
Piece.cs: helper methods for piece encoding and decoding 
Square.cs: helper utilities for square indexing and coordinate conversion
MoveGenerator.cs: generates pseudo-legal moves and filters illegal ones
MoveValidator.cs: optional helper for legality checks if kept separate
FenUtility.cs: loads positions from FEN and exports positions to FEN
GameRules.cs: helper rules such as attack detection, check, checkmate, stalemate, and promotion handling
AI Layer
Search.cs: handles minimax, alpha-beta pruning, and later search optimizations
Evaluator.cs: scores positions according to the active evaluation features
MoveOrdering.cs: orders moves to improve pruning efficiency
SearchSettings.cs: stores configurable search parameters
SearchResult.cs: stores best move, evaluation, node count, and search statistics
later: TranspositionTable.cs
later: OpeningBook.cs 
Script Goals
GameManager: control the flow of the match
Board: store and update complete game state
BoardState: store minimal reversible state for undo during search

Immediate Priority
1. Refactor board representation to bit-flag pieces
2. Rebuild move application and undo around the new representation
3. Connect board state to 2D sprite rendering
4. Re-enable baseline search and evaluation
5. Benchmark V1 and V2
Thinking time: instead of MoveGenerator trying to generate all pseudolegal moves and then filtering them out, keep track of all enemy possible moves, log squares where king is in check or has a piece pinned. 

Bug: move oscillation
Evaluator.cs is still mostly material + piece-square tables, so if two squares score similarly the engine doesn’t strongly care which one it sits on.
MinimaxAB.cs searches by cloning positions and scoring leaf states, but it has no real strategic concept like progress, space gain, king pressure, pawn structure improvement, or zugzwang awareness.
If one move improves nothing and the reverse move also improves nothing, the search can keep seeing both as acceptable, especially in quiet endgames.
Before the recent changes, there was no strong punishment for immediate reversals or repeated lines, so back-and-forth moves were effectively cheap.
Ideas for stopping this
Passed pawn for pawn advancement in the endgame
Penalty for underdeveloping
King pressure and attack zones

Baseline model: minimax with alpha beta pruning (could test wo alpha beta pruning to test how efficient it is) 

3 subprojects now
Core engine
Match experiment infra
Analysis pipeline (if time permits)

Main goal as of now: build chess infra for legal moves, compare engine versions, run ai vs ai matches, batch games, log data, and analyze results

Chess infra
Goal 1: piece and move representation; finalize piece.cs, move.cs, and moveFlags.cs 
Goal 2: 	boardstate and undo/redo; board.cs holds all board data, boardstate.cs holds undo data, makemove() and undomove() for searching
Goal 3: fen setup; loading starting pos with fen, load custom pos, export fen (optional, but will need for csv), parse board layout, castling rights, en passant, clocks (maybe)
Goal 4: legal move gen; needs upgrading

Engine and version sys
Goal 5: baseline search; needs clean baseline (minimax + alpha beta, with endgame behavior and possibly openings) and returns eval + stats
Goal 6: make search measurable by returning best move, eval, nodes searched, time taken, depth reached
Goal 7: controlled alpha beta test comparing pure minimax to alpha beta pruning added; collecting nodes time best move depth and legal moves at root

Match manager
Goal 8: single ai vs ai manager
Goal 9: match safety rules to prevent infinitely long games; max ply limit, time limit, 50 move rule, repetition detection, forced cutoff.
Goal 10: match logging; make sure each game produces sufficient data

Batch runner
Goal 11: compare 2 engines, collect results, and output stats
Goal 12: parallel game execution, 

Version ladder
Goal 13: engine version system; togglable features, make things configurable
Goal 14: add improvements after testing new version, find weaknesses or common patterns

Data pipeline
Goal 15: csv logging with relevant data
Goal 16: python script analysis

If time permits an ml extension but i doubt it

Search stats [Baseline] nodes=2112390, evals=1616050, ttHits=228378, cutoffs=213509, elapsedMs=82721.5, evaluator=Baseline_Evaluator
UnityEngine.Debug:Log (object)
Stats for baseline model at 4/14/26

Adding make and unmaking moves increased time taken by 45% 	

https://www.youtube.com/watch?v=U4ogK0MIzqk&t=514s 
this for baseline 

https://www.youtube.com/watch?v=_vqlIPDR2TU&t=22s
For future upgrades

After implementing move ordering, time taken reduced to 40000 ms, however that was still a lot of time taken so i decided to look for any redundancies causing expensive time use. 
Found duplicating min and max loops in minimaxab.cs -> put into shared loop
Moved depth == 0 eval before legal move gen 
Removed redundant capture sort because of new move ordering 
Remaining expensive spots I’d tackle next:

isInCheck scans for king every time
BoardState.cs:255 scans all 64 squares. Since getLegalMoves calls isInCheck after every pseudo move, this happens constantly. Next optimization: cache whiteKingSquare and blackKingSquare in BoardState.

Move generation allocates lots of lists
MoveGenerator.cs:102 creates unfilteredMoves, then every piece method returns another List<Move>. Cleaner/faster path: pass one destination list into piece move generators instead of returning new lists.

Mobility eval is still very expensive if enabled
Evaluator.cs:69 calls full legal move generation twice per evaluation. Keep mobilityWeight = 0, or later replace it with pseudo-legal mobility.

AIController generates legal moves before search
AIController.cs:82 checks legal moves, then FindBestMove generates them again. You can eventually let FindBestMove be the single source and return hasMove = false.

GameManager has repeated legal move entry points
GameManager.cs:169, :192, and :215 each snapshot and generate all legal moves. Fine for UI, but if it feels sluggish, cache legal moves for the current turn and invalidate after a move.

Basically, legal move generation, mobility eval, and checking for king

Next steps along with optimization
1. Small opening book
2. Development bonus for knights/bishops leaving back rank
3. Early queen penalty
4. Castling bonus
5. Tune piece-square tables

Before all that i need to implement a fen utility 

Roadmap as of now:
1. FEN loader
2. Perft tests
3. Fix/search benchmark positions
4. Quiescence search
5. Opening book
6. Development + castling eval
7. Zobrist hashing + transposition table
8. Killer moves/history heuristic
9. King-square cache
10. Endgame heuristics
11. Bitboard or hybrid bitboard later

Optimizations to do for legal move generation:
cache king square
avoid allocating new lists per piece
reuse move lists
generate only captures for quiescence
eventually bitboards

endgame script notes
Fen string used to test: 1K6/P7/8/k7/8/8/8/2R5 w - - 0 1
Used to test how ai responds to k vs K + P (promotion to Q) + R
First implemented endgame.cs, logic is gives more points when king has less moves to play and is near the edge. 
Before implementation, ai would just oscillate between moves since it is using heuristic and trying to decide which process of moves are best. 
Found the ai making moves to keep the king with least amount of moves to play but trapping it in the middle of the board
k tries to break out to get ground closer to the middle but R blocks off paths and when k attacks R R moves out of way and process repeats over and over again
Q not being used to bring k to corner, only blocking its path to the edge
Thus i decided to make it not use the heuristic during endgames like these and instead give it a discrete process in fighting these endgames
first pushing k to edge then using Q + R to limit moves king can play until 0
Test Fen string 2: 8/8/8/8/8/1k6/1Q6/1K6 w - - 0 1 now K vs K + Q
Even with this implementation before, the ai is not using the king with the checkmate which is necessary, only 


Mobility is too expensive because it generated legal moves for both colors, switching  to a pseudolegal move generator to make things less expensive

Also adding a bonus for pieces that develop like knights bishops and pawns

EXPERIMENT TIME:
Ordered as an ablation ladder to test impact of each added technique
Getting a lot of draws when running initial setup with v1 vs v2, could be:
1. Engines are too similar
2. Search depth/think time is too low to convert advantages
3. Evaluation does not strongly reward winning progress
4. The 100-move cap catches games before conversion
5. The engine avoids losing but does not know how to finish

Fine for time improvement techniques but for strength we need a diff way of evaluating:
Average eval advantage
Material advantage at move 40
Piece activity
Number of legal moves/mobility
How often one engine reaches +300 eval
Tactical puzzle accuracy
Checkmate conversion tests

Adjust experiment
Search-efficiency tests:
Use self-play batches.
Draws are okay.

Evaluation-strength tests:
Use fixed positions and puzzle sets.
Measure best move found, eval, and time.

Conversion tests:
Start from winning positions.
See if the engine actually finishes.

Categories for setups:
Opening positions after 4-8 moves
Quiet middlegames
Tactical middlegames that are still balanced
Endgames that are close to equal
Slight advantage positions, like +0.5 to +1.0

50 starting FENs
2 games per FEN
Swap colors each time
100 total games per matchup

Set A: Balanced FENs
Purpose: general engine strength

Set B: Tactical FENs
Purpose: search/evaluation accuracy

Set C: Conversion FENs
Purpose: can the engine win won positions?

To avoid results being dominated by one opening line, matches were run from a suite of balanced FEN positions. Each position was tested twice with colors reversed.

Noticed when doing v1 vs v1 matches, matches were skewed due to white side bias resulting in many wins and not many loses or draws, implemented color switching

With initial setup they just make the same moves over and over again since its heuristic, have to use random even setup 
Since engine is deterministic, results in same moves over and over again, thus needs random equal positions

Wrote 500000 FENs to Assets/Resources/equal_positions.fens
Read 50001 games, used 46954, saw 766402 candidates
Phase counts: opening_middlegame=475672, middlegame_endgame=256134, endgame=34596

Result of equal fen positions using python script and lichess sept 2015 open dataset 

Because the chess engine is deterministic, repeated matches from the same starting position are likely to produce identical or highly similar games. To make the experiment more meaningful, I needed to vary the starting positions while keeping them relatively equal, so that each engine version was tested across a wider range of chess situations rather than a single repeated opening line.

xImplemented richer CSV recording.

Changed CsvRecorder.cs to record per-move rows with:

game_id, ply, move_number, side_to_move, fen_before, move_uci, move_san, fen_after, depth, evaluation, nodes_searched, time_ms, best_move, engine/profile info, result, and termination reason.

Also added search metadata columns like:

search_algorithm, evaluation_version, quiescence_used, move_ordering_used, mobility_term, king_safety_term, used_opening_book, transposition_hits, alpha_beta_cutoffs.

Changed AIController.cs so AI moves pass search results into the recorder before applying the move.

Changed FEN.cs to support writing BoardState back to FEN for fen_before, fen_after, and starting_fen.

V1 vs V4: does search infrastructure alone help?
V1 vs V7: does mobility/eval help more than raw search?
V1 vs V8: does endgame tuning reduce bad draws/repetition?
V1 vs V9: does opening knowledge meaningfully change outcomes?
V7 vs V8: eval vs endgame specialization
V8 vs V9: later-game strength vs earlier-game guidance



Batch	Matchup	Newer Engine W-D-L	Score
20260424_044024	V4_TranspositionTable vs V1_Baseline	160-81-9	80.2%
20260424_011116	V8_Endgame vs V1_Baseline	144-79-27	73.4%
20260424_025955	V9_OpeningBook vs V1_Baseline	146-75-29	73.4%
20260423_201232	V7_Mobility vs V1_Baseline	139-82-29	72.0%

The draw rate is still high:

V4 batch: 81 / 250 draws
V7 batch: 82 / 250 draws
V8 batch: 79 / 250 draws
V9 batch: 75 / 250 draws
∂
Most of those are threefold repetition. That supports the thing you were already noticing: poorfish often finds safe loops instead of converting. So the next engine weakness is probably not “search deeper”; it is “evaluate progress better.” Especially king pressure, pawn promotion progress, avoiding repeated positions when ahead, and converting won endgames.

The V9_OpeningBook result is also kind of mislabeled as evidence: the opening book was used 0 times in that batch. Since these games start from random equal FENs, the positions are already outside normal opening-book territory. So that run tests the V9 profile’s evaluator/search settings, not the actual book.

One caution: the summary CSVs have a suspicious mismatch where the profile names and technique columns appear swapped in some rows. The per-game file looks trustworthy, so I’d base conclusions on experiment_matches.csv, not only the summary row.

I tested engine versions on 125 balanced starting positions, playing each position twice with colors swapped. Search-focused improvements produced the clearest strength gain: the transposition-table version scored about 80% against the baseline over 250 games. Later evaluation-heavy versions also beat the baseline by a wide margin, around 72-73%, but did not outperform the search-focused version in this test. Many games still ended by repetition, showing that the engine’s next major weakness is converting advantages rather than simply finding legal or materially good moves.

I excluded the opening-book version from the random-position comparison because the experiment begins from arbitrary FEN positions, where an opening book is usually irrelevant. Opening-book strength should be tested separately from normal starting positions or curated early-game positions.

V4 probably did better because it added search efficiency without changing the engine’s taste too much.

The baseline evaluator is simple: mostly material, with a little endgame mate pressure. V4_TranspositionTable keeps that simple evaluator, but adds the search stack: alpha-beta, move ordering, transposition table, adaptive depth. So it can look deeper and avoid wasting nodes, while still making decisions according to the same basic material logic.

The later versions add more evaluation terms like PST, development, mobility, and stronger endgame weights. Those make the engine more opinionated. That can help, but it can also mislead the search from random positions. In weird equal FENs, a piece-square table or development bonus might value “nice-looking” chess principles that are less important than concrete tactics or promotion races.

V4: “same simple goals, searches them better”
V7/V8: “searches well, but now has extra preferences that may sometimes distort the choice”
V9: not really testing the book here, so ignore that part



 


