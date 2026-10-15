using UnityEngine;
using TMPro;
using System;

public enum CameraAngle
{
    menu = 0,
    whiteTeam = 1,
    blackTeam = 2
}

public class GameUI : MonoBehaviour
{
    public Chessboard chbrd;
    public static GameUI Instance {set; get;}

    public Server server;
    public Client client;

    [SerializeField] private Animator menuAnimator;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private GameObject[] cameraAngles;

    public Action<bool> SetLocalGame;

    private void Awake(){
        Instance = this;
        RegisterEvents();
    }
    public void ChangeCamera (CameraAngle index){
        for(int i = 0; i < cameraAngles.Length; i++) {
            cameraAngles[i].SetActive(false);
        }
        cameraAngles[(int)index].SetActive(true);
    }

    public void OnLocalGameBut () {
        menuAnimator.SetTrigger("LocalTeamMenu");
    }
    public void OnLocalTeamBut (int typeIndex) {
        switch (typeIndex)
        {
            case 0: chbrd.playerCount = -1; break;
            case 1: chbrd.playerCount = 0; break;
        }
        menuAnimator.SetTrigger("LocalDiffMenu");
    }
    public void OnLocalDiffBut (int typeIndex) {
        switch (typeIndex)
        {
            case 0: chbrd.difficulty = 0; break;
            case 1: chbrd.difficulty = 1; break;
            case 2: chbrd.difficulty = 2; break;
            case 3: chbrd.difficulty = 3; break;
        }
        menuAnimator.SetTrigger("InGameMenu");
        SetLocalGame?.Invoke(true);
        server.Init(5454);
        client.Init("127.0.0.1", 5454);
    }
    public void OnOnlineGameBut () {
        menuAnimator.SetTrigger("OnlineMenu");
    }
    public void OnOnlineHostBut () {
        SetLocalGame?.Invoke(false);
        server.Init(5454);
        client.Init("127.0.0.1", 5454);
        menuAnimator.SetTrigger("HostMenu");
    }
    public void OnOnlineConnectBut () {
        SetLocalGame?.Invoke(false);
        client.Init(addressInput.text, 5454);
        Debug.Log("Connect");
    }
    public void OnOnlineBackBut () {
        menuAnimator.SetTrigger("StartMenu");
    }
    public void OnHostBackBut () {
        server.Shutdown();
        client.Shutdown();
        menuAnimator.SetTrigger("OnlineMenu");
    }
    public void OnLeaveFromGameMenu () {
        ChangeCamera(CameraAngle.menu);
        menuAnimator.SetTrigger("StartMenu");
    }

    private void RegisterEvents () {
        NetUtility.C_START_GAME += OnStartGameClient;
    }
    private void UnRegisterEvents () {
        
    }
    private void OnStartGameClient (NetMessage obj) {
        menuAnimator.SetTrigger("InGameMenu");
    }
}