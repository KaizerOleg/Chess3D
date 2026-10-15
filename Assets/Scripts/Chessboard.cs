using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport;

public enum SpecialMove{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class Chessboard : MonoBehaviour
{
    [SerializeField] public float deathSize = 0.56f;
    [SerializeField] public float deathSpace = 0.585f;
    [SerializeField] public float tileSize = 1.08f;
    [SerializeField] public float yOffset = 0.301f;

    [Header("Art")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] public float dragOffset = 1f;
    [SerializeField] private GameObject victoryScrean;
    [SerializeField] private Transform rematchIndicator;
    [SerializeField] private GameObject promotionPanel;
    [SerializeField] private GameObject TeamPanel;
    
    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    #region LOGIC
    public ChessPiece[,] chessPieces;
    public List<ChessPiece> deadWhite = new List<ChessPiece>();
    public List<ChessPiece> deadBlack = new List<ChessPiece>();
    public Vector3 bounds;
    public bool isWhiteTurn;
    public List<Vector2Int[]> moveList = new List<Vector2Int[]>();

    public SpecialMove specialMove;
    public Vector2Int pendingPromotionPos;
    public int pendingPromotionTeam;
    public ChessPiece CurrentDragging;
    public AI ai;
    public List<Vector2Int> availebleMoves = new List<Vector2Int>();
    public const int TILE_COUNT_X = 8;
    public const int TILE_COUNT_Y = 8;
    public GameObject[,] tiles;
    public Camera currentCamera;
    public Vector2Int currentHover;

    public int playerCount = -1;
    public int currentTeam = -1;
    private bool localGame = true;
    private bool[] playerRematch = new bool[2];
    public int difficulty = 1;
    #endregion
    
    private void Start(){
        isWhiteTurn = true;
        ai = new AI(this);
        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();

        RegisterEvents();
    }
    private void Update() {
        if (!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if(Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile","Hover","Highlight")))
        {
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            if(currentHover == -Vector2Int.one){
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            if(currentHover != -Vector2Int.one){
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availebleMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            if(Input.GetMouseButtonDown(0)){
                if(chessPieces[hitPosition.x, hitPosition.y] != null){
                    if((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn && currentTeam == 0) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn && currentTeam == 1)){
                        CurrentDragging = chessPieces[hitPosition.x, hitPosition.y];
                        availebleMoves = CurrentDragging.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                        specialMove = CurrentDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availebleMoves);
                        PreventCheck();
                        HighlightTiles();
                    }
                }
            }

            if(CurrentDragging != null && Input.GetMouseButtonUp(0)){
                Vector2Int previousPosition = new Vector2Int(CurrentDragging.currentX, CurrentDragging.currentY);
                if(ContainsValidMove(ref availebleMoves, new Vector2Int(hitPosition.x, hitPosition.y))){
                    MoveTo(previousPosition.x, previousPosition.y, hitPosition.x, hitPosition.y);

                    NetMakeMove mm = new NetMakeMove();
                    mm.originalX = previousPosition.x;
                    mm.originalY = previousPosition.y;
                    mm.destinationX = hitPosition.x;
                    mm.destinationY = hitPosition.y;
                    mm.teamID = currentTeam;
                    Client.Instance.SendToServer(mm);
                }
                else{
                    CurrentDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y, chessPieces[previousPosition.x, previousPosition.y].team));
                    CurrentDragging = null;
                    RemoveHighlightTiles(); 
                }

                if (!isWhiteTurn && currentTeam == 0 && localGame) {
                    ai.MakeRandomAIMoveSimple();
                }
                if (isWhiteTurn && currentTeam == 1 && localGame) {
                    ai.MakeRandomAIMoveSimple();
                }
            }
        }
        else
        {
            if(currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availebleMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }

            if(CurrentDragging && Input.GetMouseButtonUp(0)){
                CurrentDragging.SetPosition(GetTileCenter(CurrentDragging.currentX, CurrentDragging.currentY, chessPieces[CurrentDragging.currentX, CurrentDragging.currentY].team));
                CurrentDragging = null;
                RemoveHighlightTiles();
            }
        }

        if(CurrentDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if(horizontalPlane.Raycast(ray, out distance))
            {
                CurrentDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
            }
        }
    }
    #region Generate the board
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY){
        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, 0, (tileCountX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
        {
            for (int y = 0; y < tileCountY; y++)
            {
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
            }
        }
    }
    private GameObject GenerateSingleTile(float tileSize, int x, int y){
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[]vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y+1) * tileSize) - bounds;
        vertices[2] = new Vector3((x+1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x+1) * tileSize, yOffset, (y+1) * tileSize) - bounds;

        int[] tris = new int[] {0, 1, 2, 1, 3, 2};
        
        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }
    #endregion
    #region Spawn Pieces
    private void SpawnAllPieces(){
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        int whiteTeam = 0, blackTeam = 1;

        //White
        chessPieces[0,0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1,0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[2,0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[3,0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[4,0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[5,0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6,0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[7,0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        for (int i = 0; i < TILE_COUNT_X; i++)
        {
            chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);
        }

        //Black
        chessPieces[0,7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1,7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2,7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3,7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4,7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[5,7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6,7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7,7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        for (int i = 0; i < TILE_COUNT_X; i++)
        {
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);
        }
    }
    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team){
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();

        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];

        return cp;
    }
    #endregion
    #region Positioning
    private void PositionAllPieces(){
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(chessPieces[x,y] != null){
                    PositionSinglePiece(x,y, true);
                }
            }
        }
    }
    public void PositionSinglePiece(int x, int y, bool force = false){
        chessPieces[x,y].currentX = x;
        chessPieces[x,y].currentY = y;
        chessPieces[x,y].SetPosition(GetTileCenter(x,y,chessPieces[x,y].team), force);
    }
    private Vector3 GetTileCenter(int x, int y, int team){
        if(team == 0) {
            return new Vector3(x*tileSize, yOffset, y*tileSize) - bounds + new Vector3(tileSize / 2 - 0.3f, 0, tileSize / 2 - 0.3f);
        }
        else{
            return new Vector3(x*tileSize, yOffset, y*tileSize) - bounds + new Vector3(tileSize / 2 + 0.3f, 0, tileSize / 2 + 0.3f);
        }

    }
    #endregion
    #region HighLight
    private void HighlightTiles(){
        for (int i = 0; i < availebleMoves.Count; i++)
        {
            tiles[availebleMoves[i].x, availebleMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }
    private void RemoveHighlightTiles(){
        for (int i = 0; i < availebleMoves.Count; i++)
        {
            tiles[availebleMoves[i].x, availebleMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }
        availebleMoves.Clear();
    }
    #endregion
    #region CheckMate
    public void CheckMate(int team){
        DisplayVictory(team);
    }
    private void DisplayVictory(int WinningTeam){
        victoryScrean.SetActive(true);
        victoryScrean.transform.GetChild(WinningTeam).gameObject.SetActive(true);
    }
    public void OnRematchButton(){
        if(localGame)
        {
            NetRematch wrm = new NetRematch();
            wrm.teamID = 0;
            wrm.wantRematch = 1;
            Client.Instance.SendToServer(wrm);
            NetRematch brm = new NetRematch();
            brm.teamID = 1;
            brm.wantRematch = 1;
            Client.Instance.SendToServer(brm);
        }
        else {
            NetRematch rm = new NetRematch();
            rm.teamID = currentTeam;
            rm.wantRematch = 1;
            Client.Instance.SendToServer(rm);
        }
        
    }
    public void GameReset(){
        rematchIndicator.transform.GetChild(0).gameObject.SetActive(false);
        rematchIndicator.transform.GetChild(1).gameObject.SetActive(false);

        victoryScrean.transform.GetChild(0).gameObject.SetActive(false);
        victoryScrean.transform.GetChild(1).gameObject.SetActive(false);
        victoryScrean.SetActive(false);

        CurrentDragging = null;
        availebleMoves.Clear();
        moveList.Clear();
        playerRematch[0] = playerRematch[1] =false;

        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(chessPieces[x,y] != null){
                    Destroy(chessPieces[x,y].gameObject);
                }
                chessPieces[x,y] = null;
            }
        }

        for(int i = 0; i <deadWhite.Count; i++){
            Destroy(deadWhite[i].gameObject);
        }
        for (int i = 0; i < deadBlack.Count; i++)
        {
            Destroy(deadBlack[i].gameObject);
        }

        deadWhite.Clear();
        deadBlack.Clear();

        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;
        if (currentTeam == 1) {
            ai.MakeRandomAIMoveSimple();
        }
    }
    public void OnMenuButton(){
        NetRematch rm = new NetRematch();
        rm.teamID = 1;
        rm.wantRematch = 0;
        Client.Instance.SendToServer(rm);

        GameReset();
        GameUI.Instance.OnLeaveFromGameMenu();

        Invoke("ShutdownRelay", 2.0f);

        playerCount = -1;
        currentTeam = -1;
    }
    #endregion
    #region Special
    public void OnPromote(int typeIndex){
        promotionPanel.SetActive(false);

        ChessPiece pawn = chessPieces[pendingPromotionPos.x, pendingPromotionPos.y];
        if (pawn == null) return;

        Destroy(pawn.gameObject);
        chessPieces[pendingPromotionPos.x, pendingPromotionPos.y] = null;

        ChessPieceType chosenType = ChessPieceType.Queen; 
        switch (typeIndex)
        {
            case 0: chosenType = ChessPieceType.Queen; break;
            case 1: chosenType = ChessPieceType.Rook; break;
            case 2: chosenType = ChessPieceType.Knight; break;
            case 3: chosenType = ChessPieceType.Bishop; break;
            
        }

        ChessPiece promoted = SpawnSinglePiece(chosenType, pendingPromotionTeam);
        promoted.currentX = pendingPromotionPos.x;
        promoted.currentY = pendingPromotionPos.y;

        chessPieces[pendingPromotionPos.x, pendingPromotionPos.y] = promoted;
        promoted.SetPosition(GetTileCenter(promoted.currentX, promoted.currentY, promoted.team));

        if (!isWhiteTurn && currentTeam == 0 && localGame) {
            ai.MakeRandomAIMoveSimple();
        }
        if (isWhiteTurn && currentTeam == 1 && localGame) {
            ai.MakeRandomAIMoveSimple();
        }
    }
    public void ProcessSpecialMove () {
        if(specialMove == SpecialMove.EnPassant) {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];
            if(myPawn.currentX == enemyPawn.currentX) {
                if(myPawn.currentY == enemyPawn.currentY - 1 || myPawn.currentY == enemyPawn.currentY + 1) {
                    if(enemyPawn.team == 0)
                    {
                        deadWhite.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                        - bounds
                        + new Vector3(tileSize/2,0,tileSize/2)
                        + (Vector3.forward * deathSpace) * deadWhite.Count);
                    }
                    else{
                        deadBlack.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                        - bounds
                        + new Vector3(tileSize/2,0,tileSize/2)
                        + (Vector3.back * deathSpace) * deadBlack.Count);
                    }
                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                }
            }
        }

        if (specialMove == SpecialMove.Castling){
            var newMove = moveList[moveList.Count - 1];
            ChessPiece king = chessPieces[newMove[1].x, newMove[1].y];

            int dx = newMove[1].x - newMove[0].x;

            if (dx == 2)
            {
                ChessPiece rook = chessPieces[7, newMove[1].y];
                chessPieces[5, newMove[1].y] = rook;
                chessPieces[7, newMove[1].y] = null;

                rook.SetPosition(
                    GetTileCenter(5, newMove[1].y, rook.team)
                );
                rook.currentX = 5;
                rook.currentY = newMove[1].y;
            }

            if (dx == -2)
            {
                ChessPiece rook = chessPieces[0, newMove[1].y];
                chessPieces[3, newMove[1].y] = rook;
                chessPieces[0, newMove[1].y] = null;

                rook.SetPosition(
                    GetTileCenter(3, newMove[1].y, rook.team)
                );
                rook.currentX = 3;
                rook.currentY = newMove[1].y;
            }
        }
        
        if (specialMove == SpecialMove.Promotion)
        {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece pawn = chessPieces[newMove[1].x, newMove[1].y];

            if (pawn != null && pawn.type == ChessPieceType.Pawn)
            {
                pendingPromotionPos = new Vector2Int(pawn.currentX, pawn.currentY);
                pendingPromotionTeam = pawn.team;
                CurrentDragging = null;
                promotionPanel.SetActive(true);
            }
        }
    }
    private void PreventCheck () {
        ChessPiece targetKing = null;
        for(int x = 0; x < TILE_COUNT_X; x++) {
            for(int y = 0; y < TILE_COUNT_Y; y++) {
                if(chessPieces[x,y] != null){
                    if(chessPieces[x,y].type == ChessPieceType.King){
                        if(chessPieces[x,y].team == CurrentDragging.team){
                            targetKing = chessPieces[x,y];
                        }
                    }
                }
            }
        }
        SimulateMFSP(CurrentDragging, ref availebleMoves, targetKing);
    }
    public void SimulateMFSP (ChessPiece cp, ref List<Vector2Int> moves, ChessPiece targetKing) {
        int actX = cp.currentX;
        int actY = cp.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        for (int i = 0; i < moves.Count; i++){
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPosThisSim = new Vector2Int(targetKing.currentX, targetKing.currentY);
            if(cp.type == ChessPieceType.King){
                kingPosThisSim = new Vector2Int(simX, simY);
            }
            ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
            List<ChessPiece> simAttack = new List<ChessPiece>();
            for(int x = 0; x < TILE_COUNT_X; x++) {
                for(int y = 0; y < TILE_COUNT_Y; y++) {
                    if(chessPieces[x,y] != null){
                        simulation[x,y] = chessPieces[x,y];
                        if(simulation[x,y].team != cp.team){
                            simAttack.Add(simulation[x,y]);
                        }
                    }
                }
            }

            simulation[actX, actY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;

            var deadPiece = simAttack.Find(c => c.currentX == simX && c.currentY == simY);
            if(deadPiece != null){
                simAttack.Remove(deadPiece);
            }
            List<Vector2Int> simMoves = new List<Vector2Int>();
            for(int a = 0; a < simAttack.Count; a++){
                var pieceMoves = simAttack[a].GetAvailableMoves(ref simulation, TILE_COUNT_X, TILE_COUNT_Y);
                for(int b = 0; b < pieceMoves.Count; b++){
                    simMoves.Add(pieceMoves[b]);
                }
            }
            if(ContainsValidMove(ref simMoves, kingPosThisSim))
            {   
                movesToRemove.Add(moves[i]);
            }

            cp.currentX = actX;
            cp.currentY = actY;
        }

        for(int i = 0; i < movesToRemove.Count; i++) {
            moves.Remove(movesToRemove[i]);
        }
    }
    public bool CheckForCheckmate() {
        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();
        ChessPiece targetKing = null;
        for(int x = 0; x < TILE_COUNT_X; x++) {
            for(int y = 0; y < TILE_COUNT_Y; y++) {
                if(chessPieces[x,y] != null){
                    if(chessPieces[x,y].team == targetTeam){
                        defendingPieces.Add(chessPieces[x,y]);
                        if(chessPieces[x,y].type == ChessPieceType.King){
                            targetKing = chessPieces[x,y];
                        }
                    }
                    else{
                        attackingPieces.Add(chessPieces[x,y]);
                    }
                }
            }
        }
        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for(int i = 0; i < attackingPieces.Count; i++) {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            for(int b = 0; b < pieceMoves.Count; b++){
                currentAvailableMoves.Add(pieceMoves[b]);
            }
        }
        if(ContainsValidMove(ref currentAvailableMoves, new Vector2Int(targetKing.currentX, targetKing.currentY))){
            for(int i = 0; i < defendingPieces.Count; i++) {

                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                SimulateMFSP(defendingPieces[i], ref defendingMoves, targetKing);

                if(defendingMoves.Count != 0){
                    return false;
                }
            }
            return true;
        }

        return false;
    }
    #endregion
    #region Operations
    public bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos) {
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].x == pos.x && moves[i].y == pos.y)
            {
                return true;
            }
        }
        return false;
    }
    private void MoveTo(int originalX, int originalY, int x, int y){
        ChessPiece cp = chessPieces[originalX,originalY];
        Vector2Int previousPosition = new Vector2Int(originalX, originalY);

        if(chessPieces[x, y] != null){
            ChessPiece ocp = chessPieces[x, y];

            if(cp.team == ocp.team){
                return;
            }

            if(ocp.team == 0)
            {
                if(ocp.type == ChessPieceType.King){
                    CheckMate(1);
                }

                deadWhite.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                - bounds
                + new Vector3(tileSize/2,0,tileSize/2)
                + (Vector3.forward * deathSpace) * deadWhite.Count);
            }
            else
            {
                if(ocp.type == ChessPieceType.King){
                    CheckMate(0);
                }

                deadBlack.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                - bounds
                + new Vector3(tileSize/2,0,tileSize/2)
                + (Vector3.back * deathSpace) * deadBlack.Count);
            }
        }

        chessPieces[x, y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;

        PositionSinglePiece(x,y);
        
        isWhiteTurn = !isWhiteTurn;
        // if(localGame)
        // {
            // localTeam = (localTeam == 0) ? 1 : 0;
        // }
        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x,y)});

        ProcessSpecialMove();

        CurrentDragging = null;
        RemoveHighlightTiles(); 

        if(CheckForCheckmate()){
            CheckMate(cp.team);
        }
        return;
    }
    private Vector2Int LookupTileIndex(GameObject hitInfo){
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if(tiles[x,y] == hitInfo)
                {
                    return new Vector2Int(x,y);
                }
            }
        }
        return -Vector2Int.one;
    }
    #endregion
    #region Multiplayer
    private void RegisterEvents () {
        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
        NetUtility.S_REMATCH += OnRematchServer;

        NetUtility.C_WELCOME += OnWelcomeClient;
        NetUtility.C_START_GAME += OnStartGameClient;
        NetUtility.C_MAKE_MOVE += OnMakeMoveClient;
        NetUtility.C_REMATCH += OnRematchClient;

        GameUI.Instance.SetLocalGame += OnSetLocalGame;
    }
    private void UnRegisterEvents () {
        NetUtility.S_WELCOME -= OnWelcomeServer;
        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.S_REMATCH -= OnRematchServer;

        NetUtility.C_WELCOME -= OnWelcomeClient;
        NetUtility.C_START_GAME -= OnStartGameClient;
        NetUtility.C_MAKE_MOVE -= OnMakeMoveClient;
        NetUtility.C_REMATCH -= OnRematchClient;

        GameUI.Instance.SetLocalGame -= OnSetLocalGame;
    }
    private void OnWelcomeServer (NetMessage msg, NetworkConnection cnn) {
        NetWelcome nw = msg as NetWelcome;

        nw.AssignedTeam = ++playerCount;

        Server.Instance.SendToClient(cnn, nw);

        if(playerCount == 1)
        {
            Server.Instance.Broadcast(new NetStartGame());
        }
    }
    private void OnWelcomeClient (NetMessage msg) {
        NetWelcome nw = msg as NetWelcome;
        currentTeam = nw.AssignedTeam;
        Debug.Log($"Team: {nw.AssignedTeam}");

        if(localGame){
            Server.Instance.Broadcast(new NetStartGame());
            if(currentTeam == 1){
                ai.MakeRandomAIMoveSimple();
            }
        }
    }
    public void OnStartGameClient (NetMessage obj) {
        GameUI.Instance.ChangeCamera((currentTeam == 0) ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
    }
    private void OnSetLocalGame (bool v) {
        localGame = v;
    }
    private void OnMakeMoveServer (NetMessage msg, NetworkConnection cnn) {
       NetMakeMove mm = msg as NetMakeMove;
       Server.Instance.Broadcast(mm);
    }
    private void OnMakeMoveClient (NetMessage msg) {
       NetMakeMove mm = msg as NetMakeMove;

       Debug.Log($"MM : {mm.teamID} : {mm.originalX} {mm.originalY} -> {mm.destinationX} {mm.destinationY}");

       if(mm.teamID != currentTeam)
       {
            ChessPiece target = chessPieces[mm.originalX, mm.originalY];

            availebleMoves = target.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            specialMove = target.GetSpecialMoves(ref chessPieces, ref moveList, ref availebleMoves);
            MoveTo(mm.originalX, mm.originalY, mm.destinationX, mm.destinationY);
       }
    }
    private void OnRematchServer (NetMessage msg, NetworkConnection cnn) {
       Server.Instance.Broadcast(msg);
    }
    private void OnRematchClient (NetMessage msg) {
       NetRematch rm = msg as NetRematch;

       playerRematch[rm.teamID] = rm.wantRematch == 1;

       if(rm.teamID != currentTeam)
       {
            rematchIndicator.transform.GetChild((rm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
       }
       if(playerRematch[0] && playerRematch[1])
       {
            GameReset();
       }
    }
    private void ShutdownRelay () {
        Client.Instance.Shutdown();
        Server.Instance.Shutdown();
    }
    #endregion
}

public static class ListExtensions {
        private static System.Random rng = new System.Random();

        public static void Shuffle<T>(this IList<T> list) {
            int n = list.Count;
            while (n > 1) {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
