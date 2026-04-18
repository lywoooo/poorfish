using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
    public List<Vector2Int> MovesForPiece(GameObject pieceObject)
    {
        List<Move> pieceMoves = LegalMovesFromPiece(pieceObject);
        List<Vector2Int> locations = new List<Vector2Int>(pieceMoves.Count);

        foreach (Move move in pieceMoves)
        {
            locations.Add(move.ToVector);
        }

        return locations;
    }

    public List<Move> LegalMovesForPiece(GameObject pieceObject)
    {
        return LegalMovesFromPiece(pieceObject);
    }

    public bool TryGetLegalMove(GameObject pieceObject, Vector2Int destination, out Move move)
    {
        move = default;
        int destinationSquare = BoardState.SquareIndex(destination);

        PopulateLegalMovesFromPiece(pieceObject);
        foreach (Move legalMove in moveQueryLegalMoves)
        {
            if (legalMove.to == destinationSquare)
            {
                move = legalMove;
                return true;
            }
        }

        return false;
    }

    private List<Move> LegalMovesFromPiece(GameObject pieceObject)
    {
        PopulateLegalMovesFromPiece(pieceObject);
        return new List<Move>(moveQueryLegalMoves);
    }

    private void PopulateLegalMovesFromPiece(GameObject pieceObject)
    {
        moveQueryLegalMoves.Clear();
        moveQueryCandidateMoves.Clear();

        Vector2Int gridPoint = GridForPiece(pieceObject);
        if (gridPoint.x < 0)
        {
            return;
        }

        BoardState state = BoardState.boardSnapshot();
        MoveGenerator.GetLegalMovesFromSquare(
            state,
            CurrentTurnColor,
            BoardState.SquareIndex(gridPoint),
            moveQueryLegalMoves,
            moveQueryCandidateMoves);
    }

    public void ApplyMove(Move move)
    {
        Vector2Int startGridPoint = move.FromVector;
        Vector2Int destination = move.ToVector;
        GameObject piece = PieceAtGrid(startGridPoint);
        if (piece == null)
        {
            return;
        }

        Piece pieceComponent = GetPieceComponent(piece);
        PieceColor pieceColor = GetPieceColor(piece);
        GameObject capturedPiece = null;
        EnPassantTarget = null;

        if (move.isEnPassant)
        {
            int capturedPawnRow = destination.y + (pieceColor == PieceColor.White ? -1 : 1);
            capturedPiece = PieceAtGrid(new Vector2Int(destination.x, capturedPawnRow));
        }
        else
        {
            capturedPiece = PieceAtGrid(destination);
        }

        if (capturedPiece != null)
        {
            CapturePiece(capturedPiece);
        }

        bool resetsHalfmoveClock = pieceComponent.Type == PieceType.Pawn || capturedPiece != null;
        HalfmoveClock = resetsHalfmoveClock ? 0 : HalfmoveClock + 1;

        MarkCastlingRightsLost(pieceComponent.Type, pieceColor, startGridPoint, includeKing: true);

        if (pieceComponent.Type == PieceType.Pawn)
        {
            movedPawns.Add(piece);

            if (Mathf.Abs(destination.y - startGridPoint.y) == 2)
            {
                EnPassantTarget = new Vector2Int(startGridPoint.x, (startGridPoint.y + destination.y) / 2);
            }
        }

        MovePiece(piece, destination);

        if (move.isCastling)
        {
            Vector2Int rookFrom = destination.x > startGridPoint.x
                ? new Vector2Int(7, startGridPoint.y)
                : new Vector2Int(0, startGridPoint.y);
            Vector2Int rookTo = destination.x > startGridPoint.x
                ? new Vector2Int(5, startGridPoint.y)
                : new Vector2Int(3, startGridPoint.y);

            GameObject rook = PieceAtGrid(rookFrom);
            if (rook != null)
            {
                MovePiece(rook, rookTo);
                MarkCastlingRightsLost(PieceType.Rook, GetPieceColor(rook), rookFrom, includeKing: true);
            }
        }

        if (pieceComponent.Type == PieceType.Pawn)
        {
            bool whitePromotes = pieceColor == PieceColor.White && destination.y == 7;
            bool blackPromotes = pieceColor == PieceColor.Black && destination.y == 0;
            if (whitePromotes || blackPromotes)
            {
                PromotePiece(piece, move.promotionType == PieceType.None ? PieceType.Queen : move.promotionType);
            }
        }

        LastAppliedMove = move;
        HasLastAppliedMove = true;
        if (pieceColor == PieceColor.White)
        {
            LastWhiteAppliedMove = move;
            HasLastWhiteAppliedMove = true;
        }
        else
        {
            LastBlackAppliedMove = move;
            HasLastBlackAppliedMove = true;
        }

        MoveApplied?.Invoke(startGridPoint, destination);
    }
}
