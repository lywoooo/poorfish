using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
    public void NextPlayer()
    {
        Player tempPlayer = currentPlayer;
        currentPlayer = otherPlayer;
        otherPlayer = tempPlayer;
        EvaluateTurnState();
    }

    public PieceType GetPieceType(GameObject piece)
    {
        return GetPieceComponent(piece).Type;
    }

    public PieceColor GetPieceColor(GameObject piece)
    {
        if (piece != null && pieceColors.TryGetValue(piece, out PieceColor color))
        {
            return color;
        }

        Debug.LogWarning("Requested PieceColor for an untracked piece.", piece);
        return PieceColor.White;
    }

    public void EndGame(string message, GameResultType resultType = GameResultType.DrawOther)
    {
        if (IsGameOver)
        {
            return;
        }

        IsGameOver = true;
        LastGameResultMessage = message;
        LastGameResultType = resultType;
        Debug.Log(message);
        GameEnded?.Invoke(message, resultType);

        var moveSelector = board.GetComponent<MoveSelector>();
        if (moveSelector != null)
        {
            moveSelector.AllowHumanInput = false;
        }

        foreach (var aiController in board.GetComponents<AIController>())
        {
            aiController.enabled = false;
        }
    }
    private void EvaluateTurnState()
    {
        if (IsGameOver)
        {
            return;
        }

        BoardState state = BoardState.boardSnapshot();
        List<Move> legalMoves = MoveGenerator.getLegalMoves(state, CurrentTurnColor);
        if (legalMoves.Count == 0)
        {
            if (MoveGenerator.isInCheck(state, CurrentTurnColor))
            {
                EndGame(otherPlayer.name + " wins by checkmate.",
                    otherPlayer == white ? GameResultType.WhiteWin : GameResultType.BlackWin);
            }
            else
            {
                EndGame("Draw by stalemate.", GameResultType.DrawStalemate);
            }

            return;
        }

        if (HasInsufficientMaterial(state))
        {
            EndGame("Draw by insufficient material.", GameResultType.DrawInsufficientMaterial);
        }
    }

    private bool HasInsufficientMaterial(BoardState state)
    {
        int whiteKnights = 0;
        int blackKnights = 0;
        var whiteBishopSquareColors = new List<bool>(2);
        var blackBishopSquareColors = new List<bool>(2);

        for (int col = 0; col < 8; col++)
        {
            for (int row = 0; row < 8; row++)
            {
                int piece = state.whatIsAt(col, row);
                if (PieceBits.isEmpty(piece))
                {
                    continue;
                }

                PieceType type = PieceBits.GetType(piece);
                PieceColor color = PieceBits.GetColor(piece);
                switch (type)
                {
                    case PieceType.King:
                        continue;
                    case PieceType.Bishop:
                        if (color == PieceColor.White)
                        {
                            whiteBishopSquareColors.Add(IsLightSquare(col, row));
                        }
                        else
                        {
                            blackBishopSquareColors.Add(IsLightSquare(col, row));
                        }
                        break;
                    case PieceType.Knight:
                        if (color == PieceColor.White)
                        {
                            whiteKnights++;
                        }
                        else
                        {
                            blackKnights++;
                        }
                        break;
                    default:
                        return false;
                }
            }
        }

        int whiteMinorCount = whiteKnights + whiteBishopSquareColors.Count;
        int blackMinorCount = blackKnights + blackBishopSquareColors.Count;

        if (whiteMinorCount == 0 && blackMinorCount == 0)
        {
            return true;
        }

        if (whiteMinorCount == 1 && blackMinorCount == 0)
        {
            return true;
        }

        if (whiteMinorCount == 0 && blackMinorCount == 1)
        {
            return true;
        }

        if (whiteKnights == 0 && blackKnights == 0 &&
            whiteBishopSquareColors.Count == 1 && blackBishopSquareColors.Count == 1)
        {
            return whiteBishopSquareColors[0] == blackBishopSquareColors[0];
        }

        return false;
    }

    private static bool IsLightSquare(int col, int row)
    {
        return (col + row) % 2 != 0;
    }
}
