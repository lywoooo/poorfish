using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
    private void InitialSetup()
    {
        TrySetupPositionFromFen(defaultFENString);
    }
    public void RestartMatch()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        ResetMatchState();

        if (!string.IsNullOrWhiteSpace(loadFENString))
        {
            if (TrySetupPositionFromFen(loadFENString))
            {
                return;
            }

            Debug.LogWarning("Invalid starting FEN. Falling back to the normal starting position.", this);
        }

        InitialSetup();
    }

    public bool TryLoadFenPosition(string fen)
    {
        if (!ValidateConfiguration())
        {
            return false;
        }

        if (!FEN.TryLoadFen(fen, out BoardState state))
        {
            return false;
        }

        ResetMatchState();
        SetupFromBoardState(state);
        return true;
    }

    private bool TrySetupPositionFromFen(string fen)
    {
        if (!FEN.TryLoadFen(fen, out BoardState state))
        {
            return false;
        }

        SetupFromBoardState(state);
        return true;
    }

    private void ResetMatchState()
    {
        ClearBoardState();

        pieces = new GameObject[8, 8];
        movedPawns = new HashSet<GameObject>();

        white = new Player("white", true);
        black = new Player("black", false);
        currentPlayer = white;
        otherPlayer = black;

        IsGameOver = false;
        LastGameResultMessage = null;
        LastGameResultType = GameResultType.None;
        WhiteKingMoved = false;
        WhiteKingsideRookMoved = false;
        WhiteQueensideRookMoved = false;
        BlackKingMoved = false;
        BlackKingsideRookMoved = false;
        BlackQueensideRookMoved = false;
        EnPassantTarget = null;
        HalfmoveClock = 0;
        HasLastAppliedMove = false;
        LastAppliedMove = default;
        HasLastWhiteAppliedMove = false;
        LastWhiteAppliedMove = default;
        HasLastBlackAppliedMove = false;
        LastBlackAppliedMove = default;
        positionRepetitionCounts.Clear();
    }

    private void SetupFromBoardState(BoardState state)
    {
        for (int square = 0; square < state.board.Length; square++)
        {
            int piece = state.board[square];
            if (PieceBits.isEmpty(piece))
            {
                continue;
            }

            PieceColor color = PieceBits.GetColor(piece);
            Player owner = color == PieceColor.White ? white : black;
            AddPiece(PieceBits.GetType(piece), owner, square % 8, square / 8);
        }

        currentPlayer = state.currentTurn == PieceColor.White ? white : black;
        otherPlayer = currentPlayer == white ? black : white;
        ApplyCastlingRights(state.castlingRights);
        EnPassantTarget = state.enPassantTarget >= 0
            ? new Vector2Int(state.enPassantTarget % 8, state.enPassantTarget / 8)
            : null;
        HalfmoveClock = state.halfmoveClock;
        HasLastAppliedMove = state.hasLastMove;
        LastAppliedMove = state.lastMove;
        HasLastWhiteAppliedMove = state.hasLastWhiteMove;
        LastWhiteAppliedMove = state.lastWhiteMove;
        HasLastBlackAppliedMove = state.hasLastBlackMove;
        LastBlackAppliedMove = state.lastBlackMove;
        RegisterCurrentPosition();
        EvaluateTurnState();
    }

    private void ApplyCastlingRights(CastlingRights rights)
    {
        bool whiteKingside = (rights & CastlingRights.WhiteKingside) != 0;
        bool whiteQueenside = (rights & CastlingRights.WhiteQueenside) != 0;
        bool blackKingside = (rights & CastlingRights.BlackKingside) != 0;
        bool blackQueenside = (rights & CastlingRights.BlackQueenside) != 0;

        WhiteKingMoved = !whiteKingside && !whiteQueenside;
        WhiteKingsideRookMoved = !whiteKingside;
        WhiteQueensideRookMoved = !whiteQueenside;
        BlackKingMoved = !blackKingside && !blackQueenside;
        BlackKingsideRookMoved = !blackKingside;
        BlackQueensideRookMoved = !blackQueenside;
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

    private void ClearBoardState()
    {
        var trackedPieces = new List<GameObject>(piecePositions.Keys);
        foreach (GameObject trackedPiece in trackedPieces)
        {
            if (trackedPiece != null)
            {
                Destroy(trackedPiece);
            }
        }

        piecePositions.Clear();
        pieceComponentCache.Clear();
        pieceColors.Clear();
        pieceOwners.Clear();

        if (pieces == null)
        {
            return;
        }

        for (int col = 0; col < 8; col++)
        {
            for (int row = 0; row < 8; row++)
            {
                pieces[col, row] = null;
            }
        }
    }
}
