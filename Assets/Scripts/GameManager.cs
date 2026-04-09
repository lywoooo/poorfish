/*
 * Copyright (c) 2018 Razeware LLC
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * Notwithstanding the foregoing, you may not use, copy, modify, merge, publish, 
 * distribute, sublicense, create a derivative work, and/or sell copies of the 
 * Software in any work that is designed, intended, or marketed for pedagogical or 
 * instructional purposes related to programming, coding, application development, 
 * or information technology.  Permission for such use, copying, modification,
 * merger, publication, distribution, sublicensing, creation of derivative works, 
 * or sale is expressly withheld.
 *    
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public static event System.Action<Vector2Int, Vector2Int> MoveApplied;

    public Board board;

    // piece game object
    public GameObject piecePrefab;

    // white piece sprites
    public Sprite whiteKingSprite;
    public Sprite whiteQueenSprite;
    public Sprite whiteBishopSprite;
    public Sprite whiteKnightSprite;
    public Sprite whiteRookSprite;
    public Sprite whitePawnSprite;

    // black piece sprites
    public Sprite blackKingSprite;
    public Sprite blackQueenSprite;
    public Sprite blackBishopSprite;
    public Sprite blackKnightSprite;
    public Sprite blackRookSprite;
    public Sprite blackPawnSprite;

    private GameObject[,] pieces;
    private HashSet<GameObject> movedPawns;
    private readonly Dictionary<GameObject, Vector2Int> piecePositions = new Dictionary<GameObject, Vector2Int>(32);
    private readonly Dictionary<GameObject, Piece> pieceComponentCache = new Dictionary<GameObject, Piece>(32);
    private readonly Dictionary<GameObject, PieceColor> pieceColors = new Dictionary<GameObject, PieceColor>(32);
    private readonly Dictionary<GameObject, Player> pieceOwners = new Dictionary<GameObject, Player>(32);

    private Player white;
    private Player black;
    public Player currentPlayer;
    public Player otherPlayer;
    public bool IsGameOver { get; private set; }
    public PieceColor CurrentTurnColor => currentPlayer == white ? PieceColor.White : PieceColor.Black;
    public string CurrentTurnName => currentPlayer != null ? currentPlayer.name : string.Empty;
    public bool WhiteKingMoved { get; private set; }
    public bool WhiteKingsideRookMoved { get; private set; }
    public bool WhiteQueensideRookMoved { get; private set; }
    public bool BlackKingMoved { get; private set; }
    public bool BlackKingsideRookMoved { get; private set; }
    public bool BlackQueensideRookMoved { get; private set; }
    public Vector2Int? EnPassantTarget { get; private set; }

    void Awake()
    {
        instance = this;
    }

    void OnValidate()
    {
        if (board == null)
        {
            board = FindFirstObjectByType<Board>();
        }

        ValidateConfiguration();
    }

    void Start ()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        pieces = new GameObject[8, 8];
        movedPawns = new HashSet<GameObject>();

        white = new Player("white", true);
        black = new Player("black", false);

        currentPlayer = white;
        otherPlayer = black;

        InitialSetup();
    }

    private void InitialSetup()
    {
        AddPiece(PieceType.Rook, white, 0, 0);
        AddPiece(PieceType.Knight, white, 1, 0);
        AddPiece(PieceType.Bishop, white, 2, 0);
        AddPiece(PieceType.Queen, white, 3, 0);
        AddPiece(PieceType.King, white, 4, 0);
        AddPiece(PieceType.Bishop, white, 5, 0);
        AddPiece(PieceType.Knight, white, 6, 0);
        AddPiece(PieceType.Rook, white, 7, 0);

        for (int i = 0; i < 8; i++)
        {
            AddPiece(PieceType.Pawn, white, i, 1);
        }

        AddPiece(PieceType.Rook, black, 0, 7);
        AddPiece(PieceType.Knight, black, 1, 7);
        AddPiece(PieceType.Bishop, black, 2, 7);
        AddPiece(PieceType.Queen, black, 3, 7);
        AddPiece(PieceType.King, black, 4, 7);
        AddPiece(PieceType.Bishop, black, 5, 7);
        AddPiece(PieceType.Knight, black, 6, 7);
        AddPiece(PieceType.Rook, black, 7, 7);

        for (int i = 0; i < 8; i++)
        {
            AddPiece(PieceType.Pawn, black, i, 6);
        }
    }

    public void AddPiece(PieceType type, Player player, int col, int row)
    {
        GameObject pieceObject = board.AddPiece(piecePrefab, col, row);
        Piece pieceComponent = EnsurePieceComponent(pieceObject, type);
        PieceColor color = player == white ? PieceColor.White : PieceColor.Black;

        SpriteRenderer sr = pieceObject.GetComponent<SpriteRenderer>();
        sr.sprite = GetSpriteForPiece(pieceComponent.Type, color);

        player.pieces.Add(pieceObject);
        pieces[col, row] = pieceObject;
        piecePositions[pieceObject] = new Vector2Int(col, row);
        pieceComponentCache[pieceObject] = pieceComponent;
        pieceColors[pieceObject] = player == white ? PieceColor.White : PieceColor.Black;
        pieceOwners[pieceObject] = player;
    }

    public List<Vector2Int> MovesForPiece(GameObject pieceObject)
    {
        Vector2Int gridPoint = GridForPiece(pieceObject);
        if (gridPoint.x < 0)
        {
            return new List<Vector2Int>(0);
        }

        BoardState state = BoardState.boardSnapshot();
        List<ChessMove> legalMoves = MoveGenerator.getLegalMoves(state, CurrentTurnColor);
        List<Vector2Int> locations = new List<Vector2Int>(legalMoves.Count);

        foreach (ChessMove move in legalMoves)
        {
            if (move.from == gridPoint)
            {
                locations.Add(move.to);
            }
        }

        return locations;
    }

    public List<ChessMove> LegalMovesForPiece(GameObject pieceObject)
    {
        Vector2Int gridPoint = GridForPiece(pieceObject);
        if (gridPoint.x < 0)
        {
            return new List<ChessMove>(0);
        }

        BoardState state = BoardState.boardSnapshot();
        List<ChessMove> legalMoves = MoveGenerator.getLegalMoves(state, CurrentTurnColor);
        List<ChessMove> pieceMoves = new List<ChessMove>();

        foreach (ChessMove move in legalMoves)
        {
            if (move.from == gridPoint)
            {
                pieceMoves.Add(move);
            }
        }

        return pieceMoves;
    }

    public bool TryGetLegalMove(GameObject pieceObject, Vector2Int destination, out ChessMove move)
    {
        move = default;

        Vector2Int gridPoint = GridForPiece(pieceObject);
        if (gridPoint.x < 0)
        {
            return false;
        }

        BoardState state = BoardState.boardSnapshot();
        List<ChessMove> legalMoves = MoveGenerator.getLegalMoves(state, CurrentTurnColor);

        foreach (ChessMove legalMove in legalMoves)
        {
            if (legalMove.from == gridPoint && legalMove.to == destination)
            {
                move = legalMove;
                return true;
            }
        }

        return false;
    }

    public void ApplyMove(ChessMove move)
    {
        GameObject piece = PieceAtGrid(move.from);
        if (piece == null)
        {
            return;
        }

        Piece pieceComponent = GetPieceComponent(piece);
        Vector2Int startGridPoint = move.from;
        Vector2Int destination = move.to;
        GameObject capturedPiece = null;
        EnPassantTarget = null;

        if (move.isEnPassant)
        {
            int capturedPawnRow = destination.y + (GetPieceColor(piece) == PieceColor.White ? -1 : 1);
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

        UpdateMoveState(piece, pieceComponent.Type, startGridPoint);

        if (pieceComponent.Type == PieceType.Pawn)
        {
            movedPawns.Add(piece);

            if (Mathf.Abs(destination.y - startGridPoint.y) == 2)
            {
                EnPassantTarget = new Vector2Int(startGridPoint.x, (startGridPoint.y + destination.y) / 2);
            }
        }

        Move(piece, destination);

        if (move.isCastling)
        {
            GameObject rook = PieceAtGrid(move.rookFrom);
            if (rook != null)
            {
                Move(rook, move.rookTo);
                UpdateMoveState(rook, PieceType.Rook, move.rookFrom);
            }
        }

        if (pieceComponent.Type == PieceType.Pawn)
        {
            bool whitePromotes = GetPieceColor(piece) == PieceColor.White && destination.y == 7;
            bool blackPromotes = GetPieceColor(piece) == PieceColor.Black && destination.y == 0;
            if (whitePromotes || blackPromotes)
            {
                PromotePiece(piece, move.promotionType == PieceType.None ? PieceType.Queen : move.promotionType);
            }
        }

        MoveApplied?.Invoke(startGridPoint, destination);
    }

    public void Move(GameObject piece, Vector2Int gridPoint)
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
            EndGame(currentPlayer.name + " wins!");
        }
        currentPlayer.capturedPieces.Add(pieceToCapture);
        if (pieceOwners.TryGetValue(pieceToCapture, out Player owner))
        {
            owner.pieces.Remove(pieceToCapture);
        }
        pieces[gridPoint.x, gridPoint.y] = null;
        movedPawns.Remove(pieceToCapture);
        UpdateCaptureState(capturedPieceComponent.Type, capturedColor, gridPoint);
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

    public void EndGame(string message)
    {
        if (IsGameOver)
        {
            return;
        }

        IsGameOver = true;
        Debug.Log(message);

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

    private bool ValidateConfiguration()
    {
        bool valid = true;

        if (board == null)
        {
            Debug.LogError("GameManager is missing its Board reference.", this);
            valid = false;
        }

        if (piecePrefab == null)
        {
            Debug.LogError("GameManager is missing its piece prefab reference.", this);
            valid = false;
        }
        else if (piecePrefab.GetComponent<SpriteRenderer>() == null)
        {
            Debug.LogError("GameManager piece prefab needs a SpriteRenderer.", piecePrefab);
            valid = false;
        }

        if (whiteKingSprite == null || whiteQueenSprite == null || whiteBishopSprite == null ||
            whiteKnightSprite == null || whiteRookSprite == null || whitePawnSprite == null ||
            blackKingSprite == null || blackQueenSprite == null || blackBishopSprite == null ||
            blackKnightSprite == null || blackRookSprite == null || blackPawnSprite == null)
        {
            Debug.LogError("GameManager needs all twelve piece sprites assigned.", this);
            valid = false;
        }

        return valid;
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

    private Sprite GetSpriteForPiece(PieceType type, PieceColor color)
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

    public Sprite SpriteForPiece(PieceType type, PieceColor color)
    {
        return GetSpriteForPiece(type, color);
    }

    private void UpdateMoveState(GameObject piece, PieceType type, Vector2Int from)
    {
        PieceColor color = GetPieceColor(piece);

        if (type == PieceType.King)
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

    private void UpdateCaptureState(PieceType capturedType, PieceColor capturedColor, Vector2Int at)
    {
        if (capturedType != PieceType.Rook)
        {
            return;
        }

        MarkRookMoved(capturedColor, at);
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
        pieceComponent.Type = type;

        SpriteRenderer spriteRenderer = piece.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = GetSpriteForPiece(type, GetPieceColor(piece));
        }
    }

    private void EvaluateTurnState()
    {
        if (IsGameOver)
        {
            return;
        }

        BoardState state = BoardState.boardSnapshot();
        List<ChessMove> legalMoves = MoveGenerator.getLegalMoves(state, CurrentTurnColor);
        if (legalMoves.Count == 0)
        {
            if (MoveGenerator.isInCheck(state, CurrentTurnColor))
            {
                EndGame(otherPlayer.name + " wins by checkmate.");
            }
            else
            {
                EndGame("Draw by stalemate.");
            }

            return;
        }

        if (HasInsufficientMaterial(state))
        {
            EndGame("Draw by insufficient material.");
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
                BoardState.BoardPiece? piece = state.board[col, row];
                if (!piece.HasValue)
                {
                    continue;
                }

                switch (piece.Value.type)
                {
                    case PieceType.King:
                        continue;
                    case PieceType.Bishop:
                        if (piece.Value.color == PieceColor.White)
                        {
                            whiteBishopSquareColors.Add(IsLightSquare(col, row));
                        }
                        else
                        {
                            blackBishopSquareColors.Add(IsLightSquare(col, row));
                        }
                        break;
                    case PieceType.Knight:
                        if (piece.Value.color == PieceColor.White)
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
