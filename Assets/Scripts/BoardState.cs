using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

public enum PieceColor { White, Black }

public class BoardState
{
    public struct BoardPiece {
        public PieceType type;
        public PieceColor color;

        public BoardPiece(PieceType typeInputted, PieceColor colorInputted)
        {
            type = typeInputted;
            color = colorInputted;
        }

        public PieceType getPieceType()
        {
            return type;
        }

        public PieceColor GetPieceColor()
        {
            return color;
        }
    }

    public BoardPiece?[,] board;
    public PieceColor currentTurn;

    public BoardState() {
        board = new BoardPiece?[8, 8];
    }

    public static BoardState boardSnapshot() {
        var gm = GameManager.instance;
        var state = new BoardState();

        Player whitePlayer = gm.currentPlayer.name == "white" ? gm.currentPlayer : gm.otherPlayer;
        Player blackPlayer = gm.currentPlayer.name == "black" ? gm.currentPlayer : gm.otherPlayer;

        for(int col = 0; col < 8; col++) {
            for(int row = 0; row < 8; row++) {
                GameObject pieceSnapshotted = gm.PieceAtGrid(new Vector2Int(col, row));

                if(pieceSnapshotted == null) continue;

                PieceColor pieceSnapshottedColor = whitePlayer.pieces.Contains(pieceSnapshotted) ? PieceColor.White : PieceColor.Black;

                state.board[col, row] = new BoardPiece(pieceSnapshotted.GetComponent<Piece>().type, pieceSnapshottedColor);
            }
        }

        state.currentTurn = gm.currentPlayer.name == "white" ? PieceColor.White : PieceColor.Black;

        return state;
    }

    public BoardState cloneBoard() {
        var clone = new BoardState {currentTurn = currentTurn};

        for(int col = 0; col < 8; col++) {
            for(int row = 0; row < 8; row++) {
                clone.board[col, row] = board[col, row];
            }
        }

        return clone;
    }

    public void applyMove(ChessMove move) {
        var piece = board[move.from.x, move.from.y];
        board[move.to.x, move.to.y] = piece;
        board[move.from.x, move.from.y] = null;

        if(piece == null) return;

        if(piece.Value.type == PieceType.Pawn) {
            bool whitePromotes = piece.Value.color == PieceColor.White && move.to.y == 7;
            bool blackPromotes = piece.Value.color == PieceColor.Black && move.to.y == 0;

            if (whitePromotes || blackPromotes) board[move.to.x, move.to.y] = new BoardPiece(PieceType.Queen, piece.Value.color);
        }
    }

    public bool inBounds(int col, int row)
    {
        return col >= 0 && col <= 7 && row >= 0 && row <= 7;
    }

    public BoardPiece? whatIsAt(int col, int row) {
        return inBounds(col, row) ? board[col, row] : null;
    }

    public void switchTurn() {
        currentTurn = currentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    public PieceColor opponent(PieceColor color) {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    public Vector2Int findKing(PieceColor color)
    {
        for (int col = 0; col < 8; col++)
            for (int row = 0; row < 8; row++)
            {
                var currentTile = board[col, row];
                if (currentTile.HasValue && currentTile.Value.type == PieceType.King && currentTile.Value.color == color) return new Vector2Int(col, row);
            }
        return new Vector2Int(-1, -1);
    }

    public 
}
