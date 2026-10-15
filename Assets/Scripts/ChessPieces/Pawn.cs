using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pawn : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY){
        List<Vector2Int> r = new List<Vector2Int>();

        int direction = (team == 0) ? 1 : -1;

        //One to front
        if(board[currentX,currentY + direction] == null){
            r.Add(new Vector2Int(currentX, currentY + direction));
        }
        //Two to front
        if(board[currentX,currentY + direction] == null){
            if(team == 0 && currentY == 1 && board[currentX, currentY + (direction * 2)] == null){
                r.Add(new Vector2Int(currentX, currentY +(direction * 2)));
            }
            if(team == 1 && currentY == 6 && board[currentX, currentY + (direction * 2)] == null){
                r.Add(new Vector2Int(currentX, currentY +(direction * 2)));
            }
        }
        //Kill
        if(currentX != tileCountX-1){
            if(board[currentX + 1, currentY + direction] != null && board[currentX + 1, currentY + direction].team != team){
                r.Add(new Vector2Int(currentX + 1, currentY + direction));
            }
        }
        if(currentX != 0){
            if(board[currentX - 1, currentY + direction] != null && board[currentX - 1, currentY + direction].team != team){
                r.Add(new Vector2Int(currentX - 1, currentY + direction));
            }
        }
        return r;
    }

    public override SpecialMove GetSpecialMoves (ref ChessPiece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availebleMoves){
        int direction = (team == 0) ? 1 : -1;
        //En Passant
        if(moveList.Count > 0){
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            if(board[lastMove[1].x, lastMove[1].y].type == ChessPieceType.Pawn){
                if(Mathf.Abs(lastMove[0].y - lastMove[1].y) == 2) {
                    if(board[lastMove[1].x, lastMove[1].y].team != team){
                        if(lastMove[1].y == currentY){
                            if(lastMove[1].x == currentX - 1){
                                availebleMoves.Add(new Vector2Int(currentX - 1, currentY + direction));
                                return SpecialMove.EnPassant;
                            }
                            if(lastMove[1].x == currentX + 1){
                                availebleMoves.Add(new Vector2Int(currentX + 1, currentY + direction));
                                return SpecialMove.EnPassant;
                            }
                        }
                    }
                }
            }
        }
        //promotion
        if (moveList.Count > 0)
        {
            if (board[currentX, currentY].type == ChessPieceType.Pawn)
            {
                int newY = currentY + direction;
                if (newY == 0 || newY == 7)
                {
                    if (board[currentX, newY] == null)
                    {
                        availebleMoves.Add(new Vector2Int(currentX, newY));
                        return SpecialMove.Promotion;
                    }
                }

                if (currentX - 1 >= 0 && newY == 0 || newY == 7)
                {
                    if (board[currentX - 1, newY] != null &&
                        board[currentX - 1, newY].team != board[currentX, currentY].team)
                    {
                        availebleMoves.Add(new Vector2Int(currentX - 1, newY));
                        return SpecialMove.Promotion;
                    }
                }

                if (currentX + 1 < 8 && newY == 0 || newY == 7)
                {
                    if (board[currentX + 1, newY] != null &&
                        board[currentX + 1, newY].team != board[currentX, currentY].team)
                    {
                        availebleMoves.Add(new Vector2Int(currentX + 1, newY));
                        return SpecialMove.Promotion;
                    }
                }
            }
        }
        
        return SpecialMove.None;
    }
}
