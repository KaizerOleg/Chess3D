using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Chessboard;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

public class AI
{
    private const int MATE_SCORE = 1000000;
    private Chessboard chbrd;
    private int aiTeam;
    public string apiKey;
    public HttpClient client;
    public string url;

    public AI(Chessboard chbrd){
        this.chbrd = chbrd;
        this.apiKey = "API KEY HERE";
        this.client = new HttpClient();
        this.client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this.apiKey);
        this.url = "https://api.openai.com/v1/chat/completions";
    }
    public void MakeRandomAIMoveSimple() {
        aiTeam = chbrd.currentTeam == 0 ? 1 : 0;
        Debug.Log("AI робить хід... (складність: " + chbrd.difficulty + ")");
        List<ChessPiece> EnemyPieces = new List<ChessPiece>();

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chbrd.chessPieces[x, y] != null && chbrd.chessPieces[x, y].team == aiTeam)
                {
                    EnemyPieces.Add(chbrd.chessPieces[x, y]);
                }
            }
        }

        for (int i = EnemyPieces.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var temp = EnemyPieces[i];
            EnemyPieces[i] = EnemyPieces[j];
            EnemyPieces[j] = temp;
        }

        // Easy (0)
        if (chbrd.difficulty == 0){
            foreach (var piece in EnemyPieces)
            {
                List<Vector2Int> moves = piece.GetAvailableMoves(ref chbrd.chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                PreventCheckAI(piece, ref moves);

                List<Vector2Int> attackMoves = new List<Vector2Int>();

                foreach (var m in moves)
                {
                    var target = chbrd.chessPieces[m.x, m.y];
                    if (target != null && target.team == chbrd.currentTeam)
                    {
                        attackMoves.Add(m);
                    }
                }

                if (attackMoves.Count > 0)
                {
                    Vector2Int bestMove = attackMoves[UnityEngine.Random.Range(0, attackMoves.Count)];
                    MovePieceAI(piece, bestMove);
                    return;
                }
            }

            foreach (var piece in EnemyPieces)
            {
                List<Vector2Int> moves = piece.GetAvailableMoves(ref chbrd.chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                PreventCheckAI(piece, ref moves);

                if (moves.Count > 0)
                {
                    Vector2Int chosenMove = moves[UnityEngine.Random.Range(0, moves.Count)];
                    MovePieceAI(piece, chosenMove);
                    return;
                }
            }
        }

        // Medium (1)
        if (chbrd.difficulty == 1)
        {
            ChessPiece bestPiece = null;
            Vector2Int bestMove = new Vector2Int();
            int bestScore = int.MinValue;

            foreach (var piece in EnemyPieces)
            {
                List<Vector2Int> moves = piece.GetAvailableMoves(ref chbrd.chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                PreventCheckAI(piece, ref moves);

                foreach (var move in moves)
                {
                    int score = EvaluateMove(piece, move);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPiece = piece;
                        bestMove = move;
                    }
                }
            }

            if (bestPiece != null)
            {
                MovePieceAI(bestPiece, bestMove);
                return;
            }
        }

        // Hard (2)
        if (chbrd.difficulty == 2)
        {
            (ChessPiece bestPiece, Vector2Int bestMove) = FindBestMoveMinimax(2);

            if (bestPiece != null)
            {
                MovePieceAI(bestPiece, bestMove);
                return;
            }
        }
        //Ти гравець в шахи, вибери найкращий хід у форматі (e2e4)": " + fen + @
        //AI
        if (chbrd.difficulty == 3)
        {
            string fen = GetFEN();
            Debug.Log("Result: " + fen);
            var requestBody = @"{
                ""model"": ""gpt-5-mini"",
                ""messages"": [
                    { ""role"": ""user"", ""content"": ""You are a chess expert, the pieces on the board are:" + fen + @", analyze the best move in this situation and return it in long algebraic notation format(e2e4, e7e5), do not use abbreviations (e4, e5, ke7 etc.). Return only the best move."" }
                ]
            }";

            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = this.client.PostAsync(url, content).Result;

            var responseString = response.Content.ReadAsStringAsync().Result;

            string result = ExtractContent(responseString);

            if (TryParseAIMove(result, out Vector2Int from, out Vector2Int to))
            {
                ChessPiece cp = chbrd.chessPieces[from.x, from.y];

                if (cp != null)
                {
                    bool moved = MovePieceAI(cp, to);

                    if (!moved)
                        Debug.Log("AI move invalid: " + from + " -> " + to);
                }
                else
                {
                    Debug.Log("AI tried to move a piece that doesn't exist: " + from);
                }
            }
            else
            {
                Debug.Log("Failed to parse AI move: " + result);
            }

            Debug.Log("Result: " + result);
        }
    }
    private bool MovePieceAI(ChessPiece cp, Vector2Int targetPos) {
        List<Vector2Int> moves = cp.GetAvailableMoves(ref chbrd.chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
        if (!chbrd.ContainsValidMove(ref moves, targetPos)) return false;

        Vector2Int previousPosition = new Vector2Int(cp.currentX, cp.currentY);

        if(chbrd.chessPieces[targetPos.x, targetPos.y] != null) {
            ChessPiece ocp = chbrd.chessPieces[targetPos.x, targetPos.y];

            if(cp.team == ocp.team){
                return false;
            }

            if(ocp.team == 0)
            {
                if(ocp.type == ChessPieceType.King){
                    chbrd.CheckMate(1);
                }

                chbrd.deadWhite.Add(ocp);
                ocp.SetScale(Vector3.one * chbrd.deathSize);
                ocp.SetPosition(new Vector3(8 * chbrd.tileSize, chbrd.yOffset, -1 * chbrd.tileSize)
                - chbrd.bounds
                + new Vector3(chbrd.tileSize/2,0,chbrd.tileSize/2)
                + (Vector3.forward * chbrd.deathSpace) * chbrd.deadWhite.Count);
            }
            else
            {
                if(ocp.type == ChessPieceType.King){
                    chbrd.CheckMate(0);
                }

                chbrd.deadBlack.Add(ocp);
                ocp.SetScale(Vector3.one * chbrd.deathSize);
                ocp.SetPosition(new Vector3(-1 * chbrd.tileSize, chbrd.yOffset, 8 * chbrd.tileSize)
                - chbrd.bounds
                + new Vector3(chbrd.tileSize/2,0,chbrd.tileSize/2)
                + (Vector3.back * chbrd.deathSpace) * chbrd.deadBlack.Count);
            }
        }

        chbrd.chessPieces[targetPos.x, targetPos.y] = cp;
        chbrd.chessPieces[previousPosition.x, previousPosition.y] = null;

        chbrd.PositionSinglePiece(targetPos.x, targetPos.y);

        chbrd.isWhiteTurn = !chbrd.isWhiteTurn;
        chbrd.moveList.Add(new Vector2Int[] { previousPosition, targetPos });

        if(chbrd.CheckForCheckmate()) { chbrd.CheckMate(cp.team); }

        Debug.Log($"MM : {aiTeam} : {previousPosition.x} {previousPosition.y} -> {targetPos.x} {targetPos.y}");

        return true;
    }
    #region easy/medium
    public void PreventCheckAI(ChessPiece aiPiece, ref List<Vector2Int> possibleMoves){
        ChessPiece targetKing = null;

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chbrd.chessPieces[x, y] != null)
                {
                    if (chbrd.chessPieces[x, y].type == ChessPieceType.King && chbrd.chessPieces[x, y].team == aiPiece.team)
                    {
                        targetKing = chbrd.chessPieces[x, y];
                    }
                }
            }
        }

        if (targetKing != null)
        {
            chbrd.SimulateMFSP(aiPiece, ref possibleMoves, targetKing);
        }
    }
    private int EvaluateMove(ChessPiece piece, Vector2Int target){
        ChessPiece targetPiece = chbrd.chessPieces[target.x, target.y];
        int score = 0;

        if (targetPiece != null && targetPiece.team != piece.team)
        {
            int attackValue = GetPieceValue(targetPiece.type);
            int defendValue = GetPieceValue(piece.type);

            bool targetDefended = IsPieceProtected(targetPiece);

            if (targetDefended)
            {
                int tradeScore = attackValue * 10 - defendValue * 10;
                if (tradeScore > 0)
                    score += tradeScore / 2;
                else
                    score -= defendValue * 8;
            }
            else
            {
                score += attackValue * 10;
            }
        }

        int centerDist = Mathf.Abs(target.x - 3) + Mathf.Abs(target.y - 3);
        score += 5 - centerDist;

        if (IsUnderAttackAfterMove(piece, target))
        {
            score -= GetPieceValue(piece.type) * 10;
        }

        if (IsCurrentlyUnderAttack(piece) && !IsPieceProtected(piece))
        {
            if (!IsUnderAttackAfterMove(piece, target))
            {
                score += GetPieceValue(piece.type) * 12;
            }
        }

        return score;
    }
    private bool IsUnderAttackAfterMove(ChessPiece cp, Vector2Int target){
        int actX = cp.currentX;
        int actY = cp.currentY;

        ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
        List<ChessPiece> simAttack = new List<ChessPiece>();

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chbrd.chessPieces[x, y] != null)
                {
                    simulation[x, y] = chbrd.chessPieces[x, y];
                    if (simulation[x, y].team != cp.team)
                    {
                        simAttack.Add(simulation[x, y]);
                    }
                }
            }
        }

        simulation[actX, actY] = null;
        var capturedPiece = simulation[target.x, target.y];
        simulation[target.x, target.y] = cp;

        int simX = target.x;
        int simY = target.y;

        List<Vector2Int> simMoves = new List<Vector2Int>();
        for (int i = 0; i < simAttack.Count; i++)
        {
            var enemyMoves = simAttack[i].GetAvailableMoves(ref simulation, TILE_COUNT_X, TILE_COUNT_Y);
            simMoves.AddRange(enemyMoves);
        }

        bool underAttack = chbrd.ContainsValidMove(ref simMoves, new Vector2Int(simX, simY));

        cp.currentX = actX;
        cp.currentY = actY;
        simulation[actX, actY] = cp;
        simulation[target.x, target.y] = capturedPiece;

        return underAttack;
    }
    private bool IsPieceProtected(ChessPiece targetPiece){
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                var ally = chbrd.chessPieces[x, y];
                if (ally != null && ally.team == targetPiece.team && ally != targetPiece)
                {
                    var allyMoves = ally.GetAvailableMoves(ref chbrd.chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                    if (chbrd.ContainsValidMove(ref allyMoves, new Vector2Int(targetPiece.currentX, targetPiece.currentY)))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    private bool IsCurrentlyUnderAttack(ChessPiece piece){
        List<Vector2Int> enemyMoves = new List<Vector2Int>();
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                var enemy = chbrd.chessPieces[x, y];
                if (enemy != null && enemy.team != piece.team)
                {
                    var moves = enemy.GetAvailableMoves(ref chbrd.chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                    enemyMoves.AddRange(moves);
                }
            }
        }

        Vector2Int pos = new Vector2Int(piece.currentX, piece.currentY);
        return chbrd.ContainsValidMove(ref enemyMoves, pos);
    }
    #endregion
    #region Values
    private int GetPieceValue(ChessPieceType type){
        switch (type)
        {
            case ChessPieceType.Pawn: return 100;
            case ChessPieceType.Knight: return 320;
            case ChessPieceType.Bishop: return 330;
            case ChessPieceType.Rook: return 500;
            case ChessPieceType.Queen: return 900;
            case ChessPieceType.King: return 20000;
            default: return 0;
        }
    }
    private int GetPawnPST(int x, int y, int team){
        int[,] pst =
        {
            { 0,  5, 10, 20, 20, 10,  5,  0 },
            { 0, 10, 20, 30, 30, 20, 10,  0 },
            { 0,  5, 10, 20, 20, 10,  5,  0 },
            { 0,  0,  0, 10, 10,  0,  0,  0 },
            { 0,  0,  0,-10,-10,  0,  0,  0 },
            { 0, -5,-10,-20,-20,-10, -5,  0 },
            { 0, -5,-10,-20,-20,-10, -5,  0 },
            { 0,  0,  0,  0,  0,  0,  0,  0 }
        };
        int py = (team == chbrd.currentTeam) ? (7 - y) : y;
        int px = Mathf.Clamp(x, 0, 7);
        return pst[py, px];
    }
    private int GetKingPST(int x, int y, int team){
        int[,] pst =
        {
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -30,-40,-40,-50,-50,-40,-40,-30 },
            { -20,-30,-30,-40,-40,-30,-30,-20 },
            { -10,-20,-20,-20,-20,-20,-20,-10 },
            {  20, 20,  0,  0,  0,  0, 20, 20 },
            {  20, 30, 10,  0,  0, 10, 30, 20 }
        };
        int ky = (team == chbrd.currentTeam) ? (7 - y) : y;
        int kx = Mathf.Clamp(x, 0, 7);
        return pst[ky, kx];
    }
    #endregion
    #region MinMax
    private class SimPiece{
        public ChessPieceType type;
        public int team;
        public int x;
        public int y;
    }
    private SimPiece[,] CopyToSimBoard(ChessPiece[,] board) { 
        SimPiece[,] sim = new SimPiece[TILE_COUNT_X, TILE_COUNT_Y]; 
        for (int x = 0; x < TILE_COUNT_X; x++) 
        { 
            for (int y = 0; y < TILE_COUNT_Y; y++) 
            { 
                if (board[x, y] != null) 
                { 
                    sim[x, y] = new SimPiece() 
                    { 
                        type = board[x, y].type, 
                        team = board[x, y].team, 
                        x = x, 
                        y = y
                    }; 
                } 
            } 
        } 
        return sim; 
    }
    private int EvaluateBoard(SimPiece[,] board){
        int score = 0;
        int mobilityBlack = 0;
        int mobilityWhite = 0;

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                SimPiece p = board[x, y];
                if (p != null)
                {
                    int value = GetPieceValue(p.type);
                    int pstBonus = 0;
                    if (p.type == ChessPieceType.Pawn) pstBonus = GetPawnPST(x, y, p.team);
                    if (p.type == ChessPieceType.King) pstBonus = GetKingPST(x, y, p.team);

                    int pieceScore = value + pstBonus;

                    score += (p.team == aiTeam ? pieceScore : -pieceScore);
                }
            }
        }

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                SimPiece p = board[x, y];
                if (p != null)
                {
                    var moves = GetSimMoves(p, board);
                    if (p.team == aiTeam) mobilityBlack += moves.Count;
                    else mobilityWhite += moves.Count;
                }
            }
        }
        score += (mobilityBlack - mobilityWhite) * 2;

        return score;
    }   
    private SimPiece SimulateMove(SimPiece[,] board, SimPiece piece, Vector2Int move){
        SimPiece captured = board[move.x, move.y];
        board[piece.x, piece.y] = null;
        piece.x = move.x;
        piece.y = move.y;
        board[move.x, move.y] = piece;
        return captured;
    }
    private int Minimax(SimPiece[,] board, int depth, bool maximizingPlayer, int alpha, int beta){
        if (depth == 0)
        {
            return EvaluateBoard(board);
        }

        bool isBlackToMove = maximizingPlayer;

        bool anyMove = false;

        if (maximizingPlayer)
        {
            int maxEval = int.MinValue;

            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    SimPiece p = board[x, y];
                    if (p != null && p.team == aiTeam)
                    {
                        List<Vector2Int> moves = GetSimMoves(p, board);
                        foreach (var move in moves)
                        {
                            anyMove = true;
                            SimPiece[,] clone = CopySimBoard(board);
                            SimPiece pieceCopy = clone[p.x, p.y];
                            SimulateMove(clone, pieceCopy, move);

                            if (IsKingInCheck(clone, aiTeam))
                                continue;

                            int eval = Minimax(clone, depth - 1, false, alpha, beta);
                            if (eval > maxEval) maxEval = eval;
                            alpha = Mathf.Max(alpha, eval);
                            if (beta <= alpha) goto AB_BREAK_MAX;
                        }
                    }
                }
            }

        AB_BREAK_MAX:

            if (!anyMove)
            {
                if (IsKingInCheck(board, aiTeam))
                {
                    return -MATE_SCORE + depth;
                }
                else
                {
                    return 0;
                }
            }

            return maxEval;
        }
        else
        {
            int minEval = int.MaxValue;

            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    SimPiece p = board[x, y];
                    if (p != null && p.team == chbrd.currentTeam)
                    {
                        List<Vector2Int> moves = GetSimMoves(p, board);
                        foreach (var move in moves)
                        {
                            anyMove = true;
                            SimPiece[,] clone = CopySimBoard(board);
                            SimPiece pieceCopy = clone[p.x, p.y];
                            SimulateMove(clone, pieceCopy, move);

                            if (IsKingInCheck(clone, chbrd.currentTeam))
                                continue;

                            int eval = Minimax(clone, depth - 1, true, alpha, beta);
                            if (eval < minEval) minEval = eval;
                            beta = Mathf.Min(beta, eval);
                            if (beta <= alpha) goto AB_BREAK_MIN;
                        }
                    }
                }
            }

        AB_BREAK_MIN:

            if (!anyMove)
            {
                if (IsKingInCheck(board, chbrd.currentTeam))
                {
                    return MATE_SCORE - depth;
                }
                else
                {
                    return 0;
                }
            }

            return minEval;
        }
    }
    private SimPiece[,] CopySimBoard(SimPiece[,] board){
        SimPiece[,] newBoard = new SimPiece[TILE_COUNT_X, TILE_COUNT_Y];
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (board[x, y] != null)
                {
                    SimPiece p = board[x, y];
                    newBoard[x, y] = new SimPiece()
                    {
                        type = p.type,
                        team = p.team,
                        x = p.x,
                        y = p.y
                    };
                }
            }
        }
        return newBoard;
    }
    private List<Vector2Int> GetSimMoves(SimPiece piece, SimPiece[,] board){
        List<Vector2Int> moves = new List<Vector2Int>();
        int x = piece.x, y = piece.y;

        switch (piece.type)
        {
            case ChessPieceType.Pawn:
                int dir = (piece.team == 0) ? 1 : -1;
                int startRow = (piece.team == 0) ? 1 : 6;
                if (IsInside(x, y + dir) && board[x, y + dir] == null)
                    moves.Add(new Vector2Int(x, y + dir));

                if (y == startRow && IsInside(x, y + dir) && IsInside(x, y + 2 * dir) && board[x, y + dir] == null && board[x, y + 2 * dir] == null)
                    moves.Add(new Vector2Int(x, y + 2 * dir));

                if (IsInside(x + 1, y + dir) && board[x + 1, y + dir] != null && board[x + 1, y + dir].team != piece.team)
                    moves.Add(new Vector2Int(x + 1, y + dir));

                if (IsInside(x - 1, y + dir) && board[x - 1, y + dir] != null && board[x - 1, y + dir].team != piece.team)
                    moves.Add(new Vector2Int(x - 1, y + dir));
                break;

            case ChessPieceType.Rook:
                moves.AddRange(GetLinearMoves(x, y, board, piece.team, new Vector2Int[] {
                    new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
                }));
                break;

            case ChessPieceType.Bishop:
                moves.AddRange(GetLinearMoves(x, y, board, piece.team, new Vector2Int[] {
                    new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
                }));
                break;

            case ChessPieceType.Queen:
                moves.AddRange(GetLinearMoves(x, y, board, piece.team, new Vector2Int[] {
                    new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
                    new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
                }));
                break;

            case ChessPieceType.Knight:
                int[,] jumps = { { 1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 }, { -1, -2 }, { -2, -1 }, { -2, 1 }, { -1, 2 } };
                for (int i = 0; i < jumps.GetLength(0); i++)
                {
                    int nx = x + jumps[i, 0];
                    int ny = y + jumps[i, 1];
                    if (IsInside(nx, ny) && (board[nx, ny] == null || board[nx, ny].team != piece.team))
                        moves.Add(new Vector2Int(nx, ny));
                }
                break;

            case ChessPieceType.King:
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx;
                        int ny = y + dy;
                        if (IsInside(nx, ny) && (board[nx, ny] == null || board[nx, ny].team != piece.team))
                            moves.Add(new Vector2Int(nx, ny));
                    }
                break;
        }

        moves = moves.OrderByDescending(m =>
        {
            if (!IsInside(m.x, m.y)) return 0;
            var target = board[m.x, m.y];
            return target != null ? GetPieceValue(target.type) : 0;
        }).ToList();

        return moves;
    }
    private (ChessPiece piece, Vector2Int to) FindBestMoveMinimax(int depth){
        SimPiece[,] simBoard = CopyToSimBoard(chbrd.chessPieces);

        Vector2Int bestFrom = new Vector2Int(-1, -1);
        Vector2Int bestTo = new Vector2Int(-1, -1);
        int bestValue = int.MinValue;

        bool inCheck = IsKingInCheck(simBoard, aiTeam);

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                SimPiece p = simBoard[x, y];
                if (p != null && p.team == aiTeam)
                {
                    List<Vector2Int> moves = GetSimMoves(p, simBoard);
                    foreach (var move in moves)
                    {
                        SimPiece[,] clone = CopySimBoard(simBoard);
                        SimPiece pieceCopy = clone[p.x, p.y];
                        SimulateMove(clone, pieceCopy, move);

                        if (IsKingInCheck(clone, aiTeam))
                            continue;

                        int eval = Minimax(clone, depth - 1, false, int.MinValue, int.MaxValue);
                        if (eval > bestValue)
                        {
                            bestValue = eval;
                            bestFrom = new Vector2Int(p.x, p.y);
                            bestTo = move;
                        }
                    }
                }
            }
        }

        Debug.Log($"[AI Minimax] Найкраща оцінка: {bestValue}, глибина: {depth}");

        if (bestFrom.x < 0)
        {
            return (null, new Vector2Int(-1, -1));
        }

        ChessPiece realPiece = chbrd.chessPieces[bestFrom.x, bestFrom.y];

        if (realPiece == null)
        {
            Debug.LogWarning("[AI Minimax] Попередження: не знайдено реальної фігури на bestFrom. Повертаю coords.");
            return (null, bestTo);
        }

        return (realPiece, bestTo);
    }
    private bool IsKingInCheck(SimPiece[,] board, int team){
        SimPiece king = null;

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                SimPiece p = board[x, y];
                if (p != null && p.team == team && p.type == ChessPieceType.King)
                {
                    king = p;
                    break;
                }
            }
            if (king != null) break;
        }

        if (king == null) return false;

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                SimPiece p = board[x, y];
                if (p != null && p.team != team)
                {
                    List<Vector2Int> enemyMoves = GetSimMoves(p, board);
                    foreach (var move in enemyMoves)
                    {
                        if (move.x == king.x && move.y == king.y)
                            return true;
                    }
                }
            }
        }

        return false;
    }
    #endregion
    #region helper
    private bool IsInside(int x, int y)
    {
        return x >= 0 && y >= 0 && x < TILE_COUNT_X && y < TILE_COUNT_Y;
    }
    private List<Vector2Int> GetLinearMoves(int x, int y, SimPiece[,] board, int team, Vector2Int[] directions)
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        foreach (var dir in directions)
        {
            int nx = x + dir.x;
            int ny = y + dir.y;

            while (IsInside(nx, ny))
            {
                if (board[nx, ny] == null)
                    moves.Add(new Vector2Int(nx, ny));
                else
                {
                    if (board[nx, ny].team != team)
                        moves.Add(new Vector2Int(nx, ny));
                    break;
                }

                nx += dir.x;
                ny += dir.y;
            }
        }

        return moves;
    }
    #endregion
    #region AIAI
    public string GetFEN(){
        string fen = "";
        
        for (int y = 7; y >= 0; y--)
        {
            int empty = 0;

            for (int x = 0; x < 8; x++)
            {
                ChessPiece p = chbrd.chessPieces[x, y];

                if (p == null)
                {
                    empty++;
                }
                else
                {
                    if (empty > 0)
                    {
                        fen += empty.ToString();
                        empty = 0;
                    }

                    char c = GetPieceLetter(p);
                    fen += c;
                }
            }

            if (empty > 0)
                fen += empty.ToString();

            if (y > 0)
                fen += '/';
        }
        string T = (aiTeam == 0) ? "white" : "black";
        // решта частин FEN можна додати при бажанні
        fen += " white team is Capital letters, your team is:" + T;
        
        return fen;
    }
    private char GetPieceLetter(ChessPiece piece){
        char letter = piece.type switch
        {
            ChessPieceType.Pawn   => 'p',
            ChessPieceType.Rook   => 'r',
            ChessPieceType.Knight => 'n',
            ChessPieceType.Bishop => 'b',
            ChessPieceType.Queen  => 'q',
            ChessPieceType.King   => 'k',
            _ => '?'
        };

        if (piece.team == 0) // білі
            letter = char.ToUpper(letter);

        return letter;
    }
    string ExtractContent(string json){
        const string key = "\"content\":";
        int start = json.IndexOf(key);
        if (start == -1) return null;

        start += key.Length;

        while (start < json.Length && (json[start] == ' ' || json[start] == '\"'))
            start++;

        int end = json.IndexOf("\"", start);
        if (end == -1) return null;

        return json.Substring(start, end - start);
    }
    private bool TryParseAIMove(string aiResponse, out Vector2Int from, out Vector2Int to){
        from = new Vector2Int();
        to = new Vector2Int();

        if (string.IsNullOrEmpty(aiResponse)) return false;

        aiResponse = aiResponse.Trim().ToLower();

        // шукаємо перший знайдений патерн типу e2e4
        var match = System.Text.RegularExpressions.Regex.Match(aiResponse, @"[a-h][1-8][a-h][1-8]");
        if (!match.Success) return false;

        string move = match.Value; // e2e4

        from = ChessNotationToCoords(move[0], move[1]);
        to   = ChessNotationToCoords(move[2], move[3]);

        return true;
    }
    #endregion
    private Vector2Int ChessNotationToCoords(char file, char rank)
    {
        int x = file - 'a';      // 'a' → 0, 'b' → 1, ..., 'h' → 7
        int y = rank - '1';      // '1' → 0, '2' → 1, ..., '8' → 7
        return new Vector2Int(x, y);
    }
}