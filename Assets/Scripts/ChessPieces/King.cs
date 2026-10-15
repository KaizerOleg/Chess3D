using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class King : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        Vector2Int[] directions = {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        foreach (var d in directions)
        {
            int x = currentX + d.x;
            int y = currentY + d.y;

            if (x >= 0 && x < tileCountX && y >= 0 && y < tileCountY)
            {
                if (board[x, y] == null || board[x, y].team != team)
                {
                    r.Add(new Vector2Int(x, y));
                }
            }
        }

        return r;
    }

    public override SpecialMove GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves)
    {
        if (type == ChessPieceType.King)
        {
            bool kingMoved = false;
            foreach (var move in moveList)
            {
                if (move[0] == new Vector2Int(currentX, currentY) || move[1] == new Vector2Int(currentX, currentY))
                {
                    kingMoved = true;
                    break;
                }
            }

            if (!kingMoved)
            {
                ChessPiece rightRook = board[board.GetLength(0) - 1, currentY];
                if (rightRook != null && rightRook.type == ChessPieceType.Rook)
                {
                    bool rookMoved = false;
                    foreach (var move in moveList)
                    {
                        if (move[0] == new Vector2Int(board.GetLength(0) - 1, currentY) ||
                            move[1] == new Vector2Int(board.GetLength(0) - 1, currentY))
                        {
                            rookMoved = true;
                            break;
                        }
                    }

                    if (!rookMoved)
                    {
                        if (board[currentX + 1, currentY] == null && board[currentX + 2, currentY] == null)
                        {
                            availableMoves.Add(new Vector2Int(currentX + 2, currentY));
                        }
                    }
                }

                ChessPiece leftRook = board[0, currentY];
                if (leftRook != null && leftRook.type == ChessPieceType.Rook)
                {
                    bool rookMoved = false;
                    foreach (var move in moveList)
                    {
                        if (move[0] == new Vector2Int(0, currentY) || move[1] == new Vector2Int(0, currentY))
                        {
                            rookMoved = true;
                            break;
                        }
                    }

                    if (!rookMoved)
                    {
                        if (board[currentX - 1, currentY] == null &&
                            board[currentX - 2, currentY] == null &&
                            board[currentX - 3, currentY] == null)
                        {
                            availableMoves.Add(new Vector2Int(currentX - 2, currentY));
                        }
                    }
                }
                return SpecialMove.Castling;
            }
        }

        return SpecialMove.None;
    }
}
