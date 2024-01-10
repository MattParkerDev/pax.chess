﻿using pax.chess.Validation;

namespace pax.chess;

public class ChessGame
{
    public ChessGame()
    {
        Id = Guid.NewGuid();
        ChessBoard = new();
    }

    /// <summary>
    /// Unique ChessGame Id
    /// </summary>
    public Guid Id { get; }

    public ChessBoard ChessBoard { get; private set; }
}

public record ChessBoard
{
    public Piece?[] Pieces { get; set; } = new Piece[64];
    public IList<BoardMove> Moves { get; set; } = new List<BoardMove>();

    public bool BlackToMove { get; private set; }
    public bool WhiteCanCastleKingSide { get; private set; } = true;
    public bool WhiteCanCastleQueenSide { get; private set; } = true;
    public bool BlackCanCastleKingSide { get; private set; } = true;
    public bool BlackCanCastleQueenSide { get; private set; } = true;
    public Position? EnPassantPosition { get; private set; }
    public int PawnHalfMoveClock { get; private set; }
    public bool IsCheck { get; private set; }
    public bool IsCheckMate { get; private set; }
    public int HalfMove { get; private set; }
    public Result Result { get; set; }

    public ChessBoard(string fen)
    {
        SetFen(fen);
        IsCheck = Validate.IsCheck(this);
        IsCheckMate = Validate.IsCheckMate(this);
    }

    public static ChessBoard FromPgn(string pgn)
    {
        ChessBoard board = new();

        var pgnMoves = PgnParser.GetPgnMoves(pgn);

        if (pgnMoves.Count == 0)
        {
            return board;
        }

        board.Result = pgnMoves[0].Result;

        for (int i = 1; i < pgnMoves.Count; i++)
        {
            var move = pgnMoves[i];
            ArgumentNullException.ThrowIfNull(move.ToPosition, $"To position not found at pgn move {move.MoveNumber}");

            Position fromPosition;
            if (move.ToPosition == Position.Unknown)
            {
                if (move.IsCastleKingSide)
                {
                    move = move with { ToPosition = new(6, board.BlackToMove ? 7 : 0) };
                }
                else if (move.IsCastleQueenSide)
                {
                    move = move with { ToPosition = new(2, board.BlackToMove ? 7 : 0) };
                }
                else
                {
                    throw new MoveException($"pgn move to position not found: {move.MoveNumber}: {move.PieceType}");
                }
                fromPosition = new(4, board.BlackToMove ? 7 : 0);
            }
            else
            {
                fromPosition = Validate.GetFromPosition(board, move, move.ToPosition);
            }

            if (fromPosition == Position.Unknown)
            {
                throw new MoveException($"pgn move from position not found: {move.MoveNumber}: {move.PieceType} to {move.ToPosition}");
            }

            var result = board.Move(fromPosition, move.ToPosition, move.Transformation);
            if (result != MoveState.Ok)
            {
                throw new MoveException($"pgn move failed at {move.MoveNumber}: {move.PieceType} from {fromPosition.ToAlgebraicNotation()} to {move.ToPosition.ToAlgebraicNotation()}");
            }
        }
        return board;
    }

    public ChessBoard()
    {
        for (int x = 0; x < 8; x++)
        {
            Pieces[1 * 8 + x] = new Piece(PieceType.Pawn, isBlack: false, x, 1);
            Pieces[6 * 8 + x] = new Piece(PieceType.Pawn, isBlack: true, x, 6);
        }
        SetupSidePieces(isBlack: false, y: 0);
        SetupSidePieces(isBlack: true, y: 7);
    }
    private void SetupSidePieces(bool isBlack, int y)
    {
        Pieces[y * 8 + 0] = new Piece(PieceType.Rook, isBlack, 0, y);
        Pieces[y * 8 + 1] = new Piece(PieceType.Knight, isBlack, 1, y);
        Pieces[y * 8 + 2] = new Piece(PieceType.Bishop, isBlack, 2, y);
        Pieces[y * 8 + 3] = new Piece(PieceType.Queen, isBlack, 3, y);
        Pieces[y * 8 + 4] = new Piece(PieceType.King, isBlack, 4, y);
        Pieces[y * 8 + 5] = new Piece(PieceType.Bishop, isBlack, 5, y);
        Pieces[y * 8 + 6] = new Piece(PieceType.Knight, isBlack, 6, y);
        Pieces[y * 8 + 7] = new Piece(PieceType.Rook, isBlack, 7, y);
    }

    private void SetFen(string fen)
    {
        ArgumentNullException.ThrowIfNull(fen);

        var fenInfos = fen.Split('/', StringSplitOptions.RemoveEmptyEntries);
        ArgumentOutOfRangeException.ThrowIfLessThan(fenInfos.Length, 8);

        var gameInfos = fenInfos[7].Split(' ', StringSplitOptions.RemoveEmptyEntries);

        fenInfos[7] = gameInfos[0];

        BlackToMove = gameInfos[1] == "b";

        if (!gameInfos[2].Contains('K', StringComparison.Ordinal))
        {
            WhiteCanCastleKingSide = false;
        }
        if (!gameInfos[2].Contains('Q', StringComparison.Ordinal))
        {
            WhiteCanCastleQueenSide = false;
        }
        if (!gameInfos[2].Contains('k', StringComparison.Ordinal))
        {
            BlackCanCastleKingSide = false;
        }
        if (!gameInfos[2].Contains('q', StringComparison.Ordinal))
        {
            BlackCanCastleQueenSide = false;
        }

        if (gameInfos[3] != "-")
        {
            int x = Map.GetIntColumn(gameInfos[3][0]);
            if (int.TryParse(gameInfos[3][1].ToString(), out int y))
            {
                EnPassantPosition = new Position(x, y - 1);
            }
            else
            {
                throw new ArgumentOutOfRangeException($"invalid enpassant info: {gameInfos[3]}");
            }
        }

        if (int.TryParse(gameInfos[4], out int pawnmoves))
        {
            PawnHalfMoveClock = pawnmoves;
        }
        else
        {
            throw new ArgumentOutOfRangeException($"invalid pawn half moves: {gameInfos[4]}");
        }

        for (int y = 0; y < fenInfos.Length; y++)
        {
            int x = 0;
            for (int i = 0; i < fenInfos[y].Length; i++)
            {
                string? interest = null;
                char c = fenInfos[y][i];
                if (int.TryParse(new string(c, 1), out int ci))
                {
                    x += ci - 1;
                }
                else
                {
                    interest = c.ToString();
                }

                if (!String.IsNullOrEmpty(interest))
                {
                    Piece piece = new(Map.GetPieceType(interest), Char.IsLower(interest[0]), x, 7 - y);
                    Pieces[piece.Position.Index()] = piece;
                }
                x += 1;
            }
        }
    }

    /// <summary>
    /// Apply move and update state and pieces
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="transformation">Required if pawn reaches last rank</param>
    /// <param name="skipValidation">Optional e.g. for engine moves</param>
    /// <returns></returns>
    public MoveState Move(Position from, Position to, PieceType transformation = PieceType.None, bool skipValidation = false)
    {
        if (!skipValidation)
        {
            var moveState = Validate.ValidateBoardMove(this, from, to);

            if (moveState != MoveState.Ok)
            {
                return moveState;
            }

            if (Validate.WouldBeCheck(this, from, to))
            {
                return MoveState.WouldBeCheck;
            }
        }

        var pieceToMove = GetPieceAt(from);

        ArgumentNullException.ThrowIfNull(pieceToMove);

        var canCastleQueenSide = pieceToMove.IsBlack ? BlackCanCastleQueenSide : WhiteCanCastleQueenSide;
        var canCastleKingSide = pieceToMove.IsBlack ? BlackCanCastleKingSide : WhiteCanCastleKingSide;

        SetCasteInfo(pieceToMove);

        var capture = GetPieceAt(to);

        var halfMoveClock = PawnHalfMoveClock;
        SetHalfMoveClock(pieceToMove, capture);
        (var isEnPassantMove, var isEnPassantCapture) = HandleEnPassant(pieceToMove, capture, to);
        var isPromotion = HandlePromotion(pieceToMove, to, transformation);
        var isNotUnique = IsNotUniquePgnMove(pieceToMove, to);

        Pieces[from.Index()] = null;
        Pieces[to.Index()] = pieceToMove;
        pieceToMove.Position = to;

        // castle
        if (pieceToMove.Type == PieceType.King && Math.Abs(from.X - to.X) > 1)
        {
            Position rookFrom;
            Position rookTo;
            if (from.X - to.X < 0)
            {
                rookFrom = BlackToMove ? new(7, 7) : new(7, 0);
                rookTo = BlackToMove ? new(5, 7) : new(5, 0);
            }
            else
            {
                rookFrom = BlackToMove ? new(0, 7) : new(0, 0);
                rookTo = BlackToMove ? new(3, 7) : new(3, 0);
            }
            var rook = GetPieceAt(rookFrom);
            ArgumentNullException.ThrowIfNull(rook);
            Pieces[rookFrom.Index()] = null;
            Pieces[rookTo.Index()] = rook;
            rook.Position = rookTo;
        }

        BlackToMove = !BlackToMove;
        HalfMove++;

        IsCheck = Validate.IsCheck(this);
        IsCheckMate = Validate.IsCheckMate(this);

        Moves.Add(new()
        {
            HalfMove = HalfMove,
            PawnHalfMoveClock = halfMoveClock,
            PieceType = isPromotion ? PieceType.Pawn : pieceToMove.Type,
            FromPosition = from,
            ToPosition = to,
            EnPassantCapture = isEnPassantCapture,
            EnPassantPawnMove = isEnPassantMove,
            IsCheck = IsCheck,
            IsCheckMate = IsCheckMate,
            Capture = isEnPassantCapture ? PieceType.Pawn :
                capture == null ? PieceType.None : capture.Type,
            IsNotUnique = isNotUnique,
            CanCasteKingSide = canCastleKingSide,
            CanCasteQueenSide = canCastleQueenSide,
            Transformation = transformation
        });

        return MoveState.Ok;
    }

    public void RevertMove()
    {
        var move = Moves.LastOrDefault();

        if (move is null)
        {
            return;
        }

        var pieceToRevert = GetPieceAt(move.ToPosition);

        ArgumentNullException.ThrowIfNull(pieceToRevert);

        Pieces[move.ToPosition.Index()] = null;
        Pieces[move.FromPosition.Index()] = pieceToRevert;
        pieceToRevert.Position = move.FromPosition;

        if (pieceToRevert.IsBlack)
        {
            BlackCanCastleKingSide = move.CanCasteKingSide;
            BlackCanCastleQueenSide = move.CanCasteQueenSide;
        }
        else
        {
            WhiteCanCastleKingSide = move.CanCasteKingSide;
            WhiteCanCastleQueenSide = move.CanCasteQueenSide;
        }

        if (move.Capture != PieceType.None)
        {
            Position revertCapturePos;
            if (move.EnPassantCapture)
            {
                revertCapturePos = new(move.ToPosition.X, pieceToRevert.IsBlack ? move.ToPosition.Y + 1 : move.ToPosition.Y - 1);
            }
            else
            {
                revertCapturePos = move.ToPosition;
            }
            Pieces[revertCapturePos.Index()] = new(move.Capture, !pieceToRevert.IsBlack, revertCapturePos.X, revertCapturePos.Y);
        }

        PawnHalfMoveClock = move.PawnHalfMoveClock;
        HalfMove--;
        BlackToMove = !BlackToMove;

        IsCheck = Validate.IsCheck(this);
        IsCheckMate = false;

        Moves.Remove(move);
    }

    private static bool HandlePromotion(Piece pieceToMove,
                                        Position to,
                                        PieceType transformation)
    {
        if (pieceToMove.Type != PieceType.Pawn)
        {
            return false;
        }

        if ((pieceToMove.IsBlack && to.Y != 0)
            || (!pieceToMove.IsBlack && to.Y != 7))
        {
            return false;
        }

        if (transformation == PieceType.None)
        {
            throw new ArgumentOutOfRangeException(nameof(transformation));
        }

        pieceToMove.Type = transformation;
        return true;
    }

    private (bool, bool) HandleEnPassant(Piece pieceToMove, Piece? capture, Position to)
    {
        if (pieceToMove.Type == PieceType.Pawn && Math.Abs(pieceToMove.Position.Y - to.Y) > 1)
        {
            var pieceLeft = GetPieceAt(new(to.X - 1, to.Y));
            var pieceRight = GetPieceAt(new(to.X + 1, to.Y));

            if ((pieceLeft is not null && pieceLeft.IsBlack != pieceToMove.IsBlack)
                || (pieceRight is not null && pieceRight.IsBlack != pieceToMove.IsBlack))
            {
                EnPassantPosition = GetEnPassantTargetPosition(BlackToMove, to);
                return (true, false);
            }
        }

        if (pieceToMove.Type == PieceType.Pawn && EnPassantPosition is not null && to.Y != pieceToMove.Position.Y && capture is null)
        {
            Pieces[new Position(EnPassantPosition.X, BlackToMove ? EnPassantPosition.Y + 1 : EnPassantPosition.Y - 1).Index()] = null;
            EnPassantPosition = null;
            return (false, true);
        }
        EnPassantPosition = null;
        return (false, false);
    }

    private static Position GetEnPassantTargetPosition(bool isBlack, Position to)
    {
        return isBlack ? new Position(to.X, to.Y + 1) : new Position(to.X, to.Y - 1);
    }

    private void SetHalfMoveClock(Piece pieceToMove, Piece? capture)
    {
        if (pieceToMove.Type == PieceType.Pawn
            || capture is not null)
        {
            PawnHalfMoveClock = 0;
        }
        else
        {
            PawnHalfMoveClock++;
        }
    }

    private void SetCasteInfo(Piece pieceToMove)
    {
        if ((BlackToMove && !BlackCanCastleKingSide && !BlackCanCastleQueenSide) ||
            (!BlackToMove && !WhiteCanCastleKingSide && !WhiteCanCastleQueenSide))
        {
            return;
        }

        if (pieceToMove.Type == PieceType.King)
        {
            if (BlackToMove)
            {
                BlackCanCastleKingSide = false;
                BlackCanCastleQueenSide = false;
            }
            else
            {
                WhiteCanCastleKingSide = false;
                WhiteCanCastleQueenSide = false;
            }
        }

        if (pieceToMove.Type == PieceType.Rook)
        {
            if (BlackToMove)
            {
                if (pieceToMove.Position.X == 0 && pieceToMove.Position.Y == 7)
                {
                    BlackCanCastleQueenSide = false;
                }
                if (pieceToMove.Position.X == 7 && pieceToMove.Position.Y == 7)
                {
                    BlackCanCastleKingSide = false;
                }
            }
            else
            {
                if (pieceToMove.Position.X == 0 && pieceToMove.Position.Y == 0)
                {
                    WhiteCanCastleQueenSide = false;
                }
                if (pieceToMove.Position.X == 7 && pieceToMove.Position.Y == 0)
                {
                    WhiteCanCastleKingSide = false;
                }
            }
        }
    }

    private bool IsNotUniquePgnMove(Piece pieceToMove, Position to)
    {
        if (pieceToMove.Type == PieceType.King)
        {
            return false;
        }

        var otherPieces = Pieces
            .OfType<Piece>()
            .Where(x => x.IsBlack == pieceToMove.IsBlack
                && x.Type == pieceToMove.Type
                && x != pieceToMove)
            .ToList();

        if (otherPieces.Count == 0)
        {
            return false;
        }

        return Validate.IsNotUniqueMove(this, to, otherPieces);
    }

    public void DisplayBoard()
    {
        Console.WriteLine("  a b c d e f g h");
        Console.WriteLine(" +----------------");
        for (int y = 7; y >= 0; y--)
        {
            Console.Write($"{y + 1}|");
            for (int x = 0; x < 8; x++)
            {
                var piece = Pieces[y * 8 + x];
                if (piece == null)
                {
                    Console.Write("  ");
                }
                else
                {
                    char pieceSymbol = GetPieceSymbol(piece);
                    Console.ForegroundColor = piece.IsBlack ? ConsoleColor.DarkGray : ConsoleColor.White;
                    Console.Write($" {pieceSymbol}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
        }
    }
    public string GetPgn()
    {
        return PgnParser.MovesToPgn(Moves.ToList());
    }

    public string GetFen()
    {
        throw new NotImplementedException();
    }

    private static char GetPieceSymbol(Piece piece)
    {
        return piece.Type switch
        {
            PieceType.Pawn => 'P',
            PieceType.Knight => 'N',
            PieceType.Bishop => 'B',
            PieceType.Rook => 'R',
            PieceType.Queen => 'Q',
            PieceType.King => 'K',
            _ => ' ',
        };
    }

    public Piece? GetPieceAt(Position pos)
    {
        if (pos.OutOfBounds)
        {
            return null;
        }

        return Pieces[pos.Index()];
    }
}