using System.Collections;
using UnityEngine;

public class AIController : MonoBehaviour
{

    // Assumes black is played by the AI
    public bool aiStartColorBlack = true;

    // 4 ply search depth
    public int searchDepth = 4;
    public float maxThinkTimeSeconds = 1.5f;

    // Move delay for cool factor
    public float moveDelay = 0.5f;
    private bool calculatingMove = false;

    void Update()
    {
        if(calculatingMove) return;

        string aiColor = aiStartColorBlack ? "black" : "white";

        if(GameManager.instance.currentPlayer.name == aiColor) {
            calculatingMove = true;
            StartCoroutine(turnEval());
        }
    }

    private IEnumerator turnEval() {
        yield return new WaitForSeconds(moveDelay);

        var gm = GameManager.instance;
        string aiColor = aiStartColorBlack ? "black" : "white";
        PieceColor aiColorEnum = aiStartColorBlack ? PieceColor.Black : PieceColor.White;
        GameObject bestPiece = null;
        ChessMove bestMove = default;
        int bestScore = aiStartColorBlack ? MinimaxAB.POS_INF : MinimaxAB.NEG_INF;
        int completedDepth = 0;

        BoardState liveState = BoardState.boardSnapshot();
        var legalMoves = MoveGenerator.getLegalMoves(liveState, aiColorEnum);

        if (legalMoves.Count == 0)
        {
            if (MoveGenerator.isInCheck(liveState, aiColorEnum))
            {
                Debug.Log(aiColor + " is checkmated.");
            }
            else
            {
                Debug.Log("stalemate");
            }

            var tileSelector = GetComponent<TileSelector>();
            var moveSelector = GetComponent<MoveSelector>();
            if (tileSelector != null) tileSelector.enabled = false;
            if (moveSelector != null) moveSelector.enabled = false;
            enabled = false;
            calculatingMove = false;
            yield break;
        }

        MinimaxAB.BeginTimedSearch(maxThinkTimeSeconds);

        for (int depth = 1; depth <= searchDepth; depth++)
        {
            GameObject depthBestPiece = null;
            ChessMove depthBestMove = default;
            int depthBestScore = aiStartColorBlack ? MinimaxAB.POS_INF : MinimaxAB.NEG_INF;

            foreach (ChessMove move in legalMoves)
            {
                var prospective = liveState.cloneBoard();
                prospective.applyMove(move);
                prospective.switchTurn();

                int score = MinimaxAB.search(prospective, depth - 1, MinimaxAB.NEG_INF, MinimaxAB.POS_INF);
                if (MinimaxAB.TimedOut())
                {
                    break;
                }

                bool prospectiveScoreIsBetter = aiStartColorBlack ? score < depthBestScore : score > depthBestScore;

                if (prospectiveScoreIsBetter || depthBestPiece == null)
                {
                    depthBestScore = score;
                    depthBestMove = move;
                    depthBestPiece = gm.PieceAtGrid(move.from);
                }
            }

            if (MinimaxAB.TimedOut())
            {
                break;
            }

            completedDepth = depth;
            bestScore = depthBestScore;
            bestMove = depthBestMove;
            bestPiece = depthBestPiece;
        }

        MinimaxAB.EndTimedSearch();

        if(bestPiece != null) {
            Vector2Int fromPos = bestMove.from;
            gm.SelectPiece(bestPiece);

            if(gm.PieceAtGrid(bestMove.to) != null) gm.CapturePieceAt(bestMove.to);

            gm.Move(bestPiece, bestMove.to);
            gm.DeselectPiece(bestPiece);

            Debug.Log(gm.currentPlayer.name + " (AI) played " + fromPos + " to " + bestMove.to + " with evaluated score of " + bestScore + " at depth " + completedDepth);
        }

        gm.NextPlayer();
        GetComponent<TileSelector>().EnterState();
        calculatingMove = false;
    }
}
