﻿namespace pax.chess;
public record Move
{
    public int HalfMoveNumber { get; init; }
    public Piece Piece { get; init; }
    public Position OldPosition { get; init; }
    public Position NewPosition { get; init; }
    public PieceType? Transformation { get; init; }
    public Piece? Capture { get; set; } = null;
    public bool IsCastle => Piece.Type == PieceType.King && Math.Abs(OldPosition.X - NewPosition.X) > 1;
    public StateInfo StateInfo { get; internal set; } = new StateInfo();
    public Evaluation? Evaluation { get; set; }
    public string PgnMove { get; set; } = String.Empty;

    public Move(Piece piece, Position newPosition, int number, PieceType? transformation = null)
    {
        Piece = piece;
        OldPosition = new Position(piece.Position.X, piece.Position.Y);
        NewPosition = new(newPosition);
        HalfMoveNumber = number;
        Transformation = transformation;
    }

    public EngineMove EngineMove => new EngineMove(OldPosition, NewPosition, Transformation);

    //public Move(Piece piece, int x, int y, PieceType? transformation = null)
    //{
    //    Piece = piece;
    //    OldPosition = new Position(piece.Position.X, piece.Position.Y);
    //    NewPosition = new Position(x, y);
    //    Transformation = transformation;
    //}

    public Move(Move move)
    {
        HalfMoveNumber = move.HalfMoveNumber;
        Piece = new(move.Piece);
        OldPosition = new(move.OldPosition);
        NewPosition = new(move.NewPosition);
        Transformation = move.Transformation;
        Capture = move.Capture == null ? null : new(move.Capture);
        StateInfo = new(move.StateInfo);
        PgnMove = move.PgnMove;
        Evaluation = move.Evaluation == null ? null : new(move.Evaluation);
    }
}
