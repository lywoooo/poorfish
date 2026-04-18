using UnityEngine;

public partial class GameManager
{
    public void AddPiece(PieceType type, Player player, int col, int row)
    {
        GameObject pieceObject = board.AddPiece(piecePrefab, col, row);
        Piece pieceComponent = EnsurePieceComponent(pieceObject, type);
        PieceColor color = player == white ? PieceColor.White : PieceColor.Black;
        pieceComponent.setPiece(PieceBits.CreatePiece(type, color), SpriteForPiece(type, color));

        player.pieces.Add(pieceObject);
        pieces[col, row] = pieceObject;
        piecePositions[pieceObject] = new Vector2Int(col, row);
        pieceComponentCache[pieceObject] = pieceComponent;
        pieceColors[pieceObject] = player == white ? PieceColor.White : PieceColor.Black;
        pieceOwners[pieceObject] = player;
    }
    public void MovePiece(GameObject piece, Vector2Int gridPoint)
    {
        Piece pieceComponent = GetPieceComponent(piece);
        if (pieceComponent.Type == PieceType.Pawn && !HasPawnMoved(piece))
        {
            movedPawns.Add(piece);
        }

        Vector2Int startGridPoint = GridForPiece(piece);
        pieces[startGridPoint.x, startGridPoint.y] = null;
        pieces[gridPoint.x, gridPoint.y] = piece;
        piecePositions[piece] = gridPoint;
        board.MovePiece(piece, gridPoint);
    }

    public void PawnMoved(GameObject pawn)
    {
        movedPawns.Add(pawn);
    }

    public bool HasPawnMoved(GameObject pawn)
    {
        return movedPawns.Contains(pawn);
    }

    public void CapturePieceAt(Vector2Int gridPoint)
    {
        GameObject pieceToCapture = PieceAtGrid(gridPoint);
        if (pieceToCapture == null)
        {
            return;
        }

        CapturePiece(pieceToCapture);
    }

    private void CapturePiece(GameObject pieceToCapture)
    {
        Vector2Int gridPoint = GridForPiece(pieceToCapture);
        if (gridPoint.x < 0)
        {
            return;
        }

        Piece capturedPieceComponent = GetPieceComponent(pieceToCapture);
        PieceColor capturedColor = GetPieceColor(pieceToCapture);

        if (capturedPieceComponent.Type == PieceType.King)
        {
            EndGame(currentPlayer == white ? "white wins!" : "black wins!",
                currentPlayer == white ? GameResultType.WhiteWin : GameResultType.BlackWin);
        }
        currentPlayer.capturedPieces.Add(pieceToCapture);
        if (pieceOwners.TryGetValue(pieceToCapture, out Player owner))
        {
            owner.pieces.Remove(pieceToCapture);
        }
        pieces[gridPoint.x, gridPoint.y] = null;
        movedPawns.Remove(pieceToCapture);
        MarkCastlingRightsLost(capturedPieceComponent.Type, capturedColor, gridPoint, includeKing: false);
        piecePositions.Remove(pieceToCapture);
        pieceComponentCache.Remove(pieceToCapture);
        pieceColors.Remove(pieceToCapture);
        pieceOwners.Remove(pieceToCapture);
        Destroy(pieceToCapture);
    }

    public bool DoesPieceBelongToCurrentPlayer(GameObject piece)
    {
        return piece != null && GetPieceColor(piece) == CurrentTurnColor;
    }

    public GameObject PieceAtGrid(Vector2Int gridPoint)
    {
        if (gridPoint.x > 7 || gridPoint.y > 7 || gridPoint.x < 0 || gridPoint.y < 0)
        {
            return null;
        }
        return pieces[gridPoint.x, gridPoint.y];
    }

    public GameObject PieceAtGrid(int square)
    {
        return square >= 0 && square < 64
            ? PieceAtGrid(new Vector2Int(square % 8, square / 8))
            : null;
    }

    public Vector2Int GridForPiece(GameObject piece)
    {
        return piece != null && piecePositions.TryGetValue(piece, out Vector2Int gridPoint)
            ? gridPoint
            : new Vector2Int(-1, -1);
    }

    public bool FriendlyPieceAt(Vector2Int gridPoint)
    {
        GameObject piece = PieceAtGrid(gridPoint);
        return piece != null && GetPieceColor(piece) == CurrentTurnColor;
    }
    private Piece GetPieceComponent(GameObject piece)
    {
        if (!pieceComponentCache.TryGetValue(piece, out Piece component) || component == null)
        {
            component = piece.GetComponent<Piece>();
            if (component == null)
            {
                component = piece.AddComponent<Piece>();
            }
            pieceComponentCache[piece] = component;
        }

        return component;
    }

    private Piece EnsurePieceComponent(GameObject pieceObject, PieceType type)
    {
        Piece component = pieceObject.GetComponent<Piece>();
        if (component == null)
        {
            component = pieceObject.AddComponent<Piece>();
        }

        component.Type = type;
        return component;
    }

    public Sprite SpriteForPiece(PieceType type, PieceColor color)
    {
        if (color == PieceColor.White)
        {
            switch(type)
            {
                case PieceType.King : return whiteKingSprite;
                case PieceType.Queen : return whiteQueenSprite;
                case PieceType.Knight : return whiteKnightSprite;
                case PieceType.Bishop : return whiteBishopSprite;
                case PieceType.Rook : return whiteRookSprite;
                case PieceType.Pawn : return whitePawnSprite;
            }
        } 
        else
        {
            switch (type)
            {
                case PieceType.King : return blackKingSprite;
                case PieceType.Queen : return blackQueenSprite;
                case PieceType.Knight : return blackKnightSprite;
                case PieceType.Bishop : return blackBishopSprite;
                case PieceType.Rook : return blackRookSprite;
                case PieceType.Pawn : return blackPawnSprite;
            }
        }
        return null;
    }

    private void MarkCastlingRightsLost(PieceType type, PieceColor color, Vector2Int from, bool includeKing)
    {
        if (includeKing && type == PieceType.King)
        {
            if (color == PieceColor.White)
            {
                WhiteKingMoved = true;
            }
            else
            {
                BlackKingMoved = true;
            }
            return;
        }

        if (type != PieceType.Rook)
        {
            return;
        }

        MarkRookMoved(color, from);
    }

    private void MarkRookMoved(PieceColor color, Vector2Int position)
    {
        if (color == PieceColor.White)
        {
            if (position.x == 0 && position.y == 0) WhiteQueensideRookMoved = true;
            if (position.x == 7 && position.y == 0) WhiteKingsideRookMoved = true;
            return;
        }

        if (position.x == 0 && position.y == 7) BlackQueensideRookMoved = true;
        if (position.x == 7 && position.y == 7) BlackKingsideRookMoved = true;
    }

    private void PromotePiece(GameObject piece, PieceType type)
    {
        Piece pieceComponent = GetPieceComponent(piece);
        pieceComponent.setPiece(PieceBits.CreatePiece(type, GetPieceColor(piece)), SpriteForPiece(type, GetPieceColor(piece)));
    }
}
