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

    public GameObject whiteKing;
    public GameObject whiteQueen;
    public GameObject whiteBishop;
    public GameObject whiteKnight;
    public GameObject whiteRook;
    public GameObject whitePawn;

    public GameObject blackKing;
    public GameObject blackQueen;
    public GameObject blackBishop;
    public GameObject blackKnight;
    public GameObject blackRook;
    public GameObject blackPawn;

    private GameObject[,] pieces;
    private HashSet<GameObject> movedPawns;
    private readonly Dictionary<GameObject, Vector2Int> piecePositions = new Dictionary<GameObject, Vector2Int>(32);
    private readonly Dictionary<GameObject, Piece> pieceComponentCache = new Dictionary<GameObject, Piece>(32);
    private readonly Dictionary<GameObject, PieceColor> pieceColors = new Dictionary<GameObject, PieceColor>(32);

    private Player white;
    private Player black;
    public Player currentPlayer;
    public Player otherPlayer;
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
        AddPiece(whiteRook, white, 0, 0);
        AddPiece(whiteKnight, white, 1, 0);
        AddPiece(whiteBishop, white, 2, 0);
        AddPiece(whiteQueen, white, 3, 0);
        AddPiece(whiteKing, white, 4, 0);
        AddPiece(whiteBishop, white, 5, 0);
        AddPiece(whiteKnight, white, 6, 0);
        AddPiece(whiteRook, white, 7, 0);

        for (int i = 0; i < 8; i++)
        {
            AddPiece(whitePawn, white, i, 1);
        }

        AddPiece(blackRook, black, 0, 7);
        AddPiece(blackKnight, black, 1, 7);
        AddPiece(blackBishop, black, 2, 7);
        AddPiece(blackQueen, black, 3, 7);
        AddPiece(blackKing, black, 4, 7);
        AddPiece(blackBishop, black, 5, 7);
        AddPiece(blackKnight, black, 6, 7);
        AddPiece(blackRook, black, 7, 7);

        for (int i = 0; i < 8; i++)
        {
            AddPiece(blackPawn, black, i, 6);
        }
    }

    public void AddPiece(GameObject prefab, Player player, int col, int row)
    {
        GameObject pieceObject = board.AddPiece(prefab, col, row);
        player.pieces.Add(pieceObject);
        pieces[col, row] = pieceObject;
        piecePositions[pieceObject] = new Vector2Int(col, row);
        pieceComponentCache[pieceObject] = pieceObject.GetComponent<Piece>();
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
        if (pieceComponent.type == PieceType.Pawn && !HasPawnMoved(piece))
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

        if (GetPieceComponent(pieceToCapture).type == PieceType.King)
        {
            Debug.Log(currentPlayer.name + " wins!");
            Destroy(board.GetComponent<TileSelector>());
            Destroy(board.GetComponent<MoveSelector>());
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
        return GetPieceComponent(piece).type;
    }

    public PieceColor GetPieceColor(GameObject piece)
    {
        return pieceColors[piece];
    }

    public bool TouchMoveEnabled()
    {
        return touchMoveEnabled;
    }

    private Piece GetPieceComponent(GameObject piece)
    {
        if (!pieceComponentCache.TryGetValue(piece, out Piece component) || component == null)
        {
            component = piece.GetComponent<Piece>();
            pieceComponentCache[piece] = component;
        }

        return component;
    }
}
