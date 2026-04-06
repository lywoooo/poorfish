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

    public Board board;
    public bool touchMoveEnabled = true;

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

    private Player white;
    private Player black;
    public Player currentPlayer;
    public Player otherPlayer;
    public bool IsGameOver { get; private set; }
    public PieceColor CurrentTurnColor => currentPlayer == white ? PieceColor.White : PieceColor.Black;

    void Awake()
    {
        instance = this;
    }

    void Start ()
    {
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
    }

    public void SelectPieceAtGrid(Vector2Int gridPoint)
    {
        GameObject selectedPiece = pieces[gridPoint.x, gridPoint.y];
        if (selectedPiece)
        {
            board.SelectPiece(selectedPiece);
        }
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

        if (GetPieceComponent(pieceToCapture).Type == PieceType.King)
        {
            EndGame(currentPlayer.name + " wins!");
        }
        currentPlayer.capturedPieces.Add(pieceToCapture);
        otherPlayer.pieces.Remove(pieceToCapture);
        pieces[gridPoint.x, gridPoint.y] = null;
        movedPawns.Remove(pieceToCapture);
        piecePositions.Remove(pieceToCapture);
        pieceComponentCache.Remove(pieceToCapture);
        pieceColors.Remove(pieceToCapture);
        Destroy(pieceToCapture);
    }

    public void SelectPiece(GameObject piece)
    {
        board.SelectPiece(piece);
    }

    public void DeselectPiece(GameObject piece)
    {
        board.DeselectPiece(piece);
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
    }

    public PieceType GetPieceType(GameObject piece)
    {
        return GetPieceComponent(piece).Type;
    }

    public PieceColor GetPieceColor(GameObject piece)
    {
        return pieceColors[piece];
    }

    public bool TouchMoveEnabled()
    {
        return touchMoveEnabled;
    }

    public void EndGame(string message)
    {
        if (IsGameOver)
        {
            return;
        }

        IsGameOver = true;
        Debug.Log(message);

        var tileSelector = board.GetComponent<TileSelector>();
        if (tileSelector != null)
        {
            tileSelector.enabled = false;
        }

        var moveSelector = board.GetComponent<MoveSelector>();
        if (moveSelector != null)
        {
            moveSelector.enabled = false;
        }

        var aiController = board.GetComponent<AIController>();
        if (aiController != null)
        {
            aiController.enabled = false;
        }
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
}
