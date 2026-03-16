using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIController : MonoBehaviour
{

    // Assumes black is played by the AI
    public bool aiStartColorBlack = true;

    // 4 ply search depth
    public int searchDepth = 4;

    // Move delay for cool factor
    public float moveDelay = 0.5f;
    private bool calculatingMove = false;

    void Update()
    {
        string aiColor = aiStartColorBlack ? "black" : "white";

        if(GameManager.instance.currentPlayer.name == aiColor) {
            calculatingMove = true;
            StartCoroutine(turnEval());
        }
    }

    private IEnumerator turnEval() {
        yield return new WaitForSeconds(moveDelay);

        var gm = GameManager.instance;
        PieceColor aiColorEnum = aiStartColorBlack ? PieceColor.Black : PieceColor.White;
        GameObject bestPiece = null;
        Vector2Int bestDestination = Vector2Int.zero;
        int bestScore = aiStartColorBlack ? MinimaxAB.POS_INF : MinimaxAB.NEG_INF;
        bool moveFound = false;

        BoardState liveState = BoardState.boardSnapshot();

        foreach(GameObject piece in gm.currentPlayer.pieces) {
            if(piece == null) continue;

            Vector2Int initialPos = gm.GridForPiece(piece);
            List<Vector2Int> destinations = gm.MovesForPiece(piece);

            foreach(Vector2Int destination in destinations) {
                var prospective = liveState.cloneBoard();
                prospective.applyMove(new ChessMove(initialPos, destination));

                if(MoveGenerator.isInCheck(prospective, aiColorEnum)) continue;

                prospective.switchTurn();

                int score = MinimaxAB.search(prospective, searchDepth - 1, MinimaxAB.NEG_INF, MinimaxAB.POS_INF);

                bool prospectiveScoreIsBetter = aiStartColorBlack ? score < bestScore : score > bestScore;

                if(prospectiveScoreIsBetter || !moveFound) {
                    bestScore = score;
                    bestPiece = piece;
                    bestDestination = destination;
                    moveFound = true;
                }
            }
        }

        if(moveFound && bestPiece != null) {
            gm.SelectPiece(bestPiece);

            if(gm.PieceAtGrid(bestDestination) != null) gm.CapturePieceAt(bestDestination);

            gm.Move(bestPiece, bestDestination);
            gm.DeselectPiece(bestPiece);

            Debug.Log(gm.otherPlayer.name + " (AI) played " + gm.GridForPiece(bestPiece) + " to " + bestDestination + " with evaluated score of " + bestScore);
        }

        else {
            Debug.Log("no legal moves (i.e. a checkmate or stalemate is predicted)");
        }

        gm.NextPlayer();
        calculatingMove = false;
    }
}
