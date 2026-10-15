using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Knight : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        Vector2Int[] moves = {
            new Vector2Int(2, 1),
            new Vector2Int(2, -1),
            new Vector2Int(-2, 1),
            new Vector2Int(-2, -1),
            new Vector2Int(1, 2),
            new Vector2Int(1, -2),
            new Vector2Int(-1, 2),
            new Vector2Int(-1, -2)
        };

        foreach (var m in moves)
        {
            int x = currentX + m.x;
            int y = currentY + m.y;

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
}
