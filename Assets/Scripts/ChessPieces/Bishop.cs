using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bishop : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        Vector2Int[] directions = {
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        foreach (var d in directions)
        {
            int x = currentX;
            int y = currentY;

            while (true)
            {
                x += d.x;
                y += d.y;

                if (x < 0 || x >= tileCountX || y < 0 || y >= tileCountY)
                    break;

                if (board[x, y] == null)
                {
                    r.Add(new Vector2Int(x, y));
                }
                else
                {
                    if (board[x, y].team != team)
                        r.Add(new Vector2Int(x, y));

                    break;
                }
            }
        }

        return r;
    }
}
