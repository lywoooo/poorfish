using System.Collections.Generic;
using UnityEngine;

public enum GameResultType
{
    None,
    WhiteWin,
    BlackWin,
    DrawStalemate,
    DrawInsufficientMaterial,
    DrawFiftyMoveRule,
    DrawThreefoldRepetition,
    DrawOther
}

public partial class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public static event System.Action<Vector2Int, Vector2Int> MoveApplied;
    public static event System.Action<string, GameResultType> GameEnded;

    public Board board;
    public GameObject piecePrefab;

    public Sprite whiteKingSprite;
    public Sprite whiteQueenSprite;
    public Sprite whiteBishopSprite;
    public Sprite whiteKnightSprite;
    public Sprite whiteRookSprite;
    public Sprite whitePawnSprite;

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
    private readonly Dictionary<string, int> positionRepetitionCounts = new Dictionary<string, int>(256);
    private readonly List<Move> moveQueryLegalMoves = new List<Move>(32);
    private readonly List<Move> moveQueryCandidateMoves = new List<Move>(32);

    private Player white;
    private Player black;
    private readonly string defaultFENString = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    [SerializeField] private string loadFENString;
    public Player currentPlayer;
    public Player otherPlayer;
    public bool IsGameOver { get; private set; }
    public string LastGameResultMessage { get; private set; }
    public GameResultType LastGameResultType { get; private set; }
    public bool HasLastAppliedMove { get; private set; }
    public Move LastAppliedMove { get; private set; }
    public bool HasLastWhiteAppliedMove { get; private set; }
    public Move LastWhiteAppliedMove { get; private set; }
    public bool HasLastBlackAppliedMove { get; private set; }
    public Move LastBlackAppliedMove { get; private set; }
    public int HalfmoveClock { get; private set; }
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

    void Start()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        RestartMatch();
    }
}
