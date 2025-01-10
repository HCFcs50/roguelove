using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;
using Cinemachine;
using UnityEngine.Rendering.Universal;

public enum CameraState {
    DEFAULT, BEDROOM
}

public enum SaveType {
    NEWGAME, SAVEDHOME, ACTIVERUN
}

public class MainMenu : MonoBehaviour
{

    public CameraState cameraState;

    [SerializeField] private PlayerController playerCont;

    [SerializeField] private GameObject saveSlots;

    [SerializeField] private SceneInfo sceneInfo;

    [SerializeField] private GameObject mainMenuScreen;

    [SerializeField] private HomeLookAt homeLookAt;

    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    [SerializeField] private PixelPerfectCamera pixelPerfectCamera;

    [SerializeField] private GameObject saveIcon;

    [SerializeField] private GameObject livingRoomPoint;
    [SerializeField] private GameObject playPoint;
    [SerializeField] private List<GameObject> montagePoints = new();

    [Header("BEDROOM STATS")]
    
    [SerializeField] private CinemachinePixelPerfect bedroomPixelPerf;

    private int montageIndex = 0;
    private bool montagePlaying = true;
    private bool stopMontage = false;
    private bool playerInPos = false;
    private bool readyToPlay = false;
    private float montageTimer = 0;

    [SerializeField] private float defaultSize;
    [SerializeField] private float transitionSize;

    [Tooltip("Montage positions change according to this speed. (in seconds)")]
    [SerializeField] private float montageSpeed;

    private void Awake() {

        // Hide save icon
        if (saveIcon) {
            saveIcon.SetActive(false);
        }

        string pathMap = Application.persistentDataPath + "/map.chris";
        string pathPlayer = Application.persistentDataPath + "/player.franny";
        string pathHome = Application.persistentDataPath + "/home.soni";

        // NEWGAME
        if (!File.Exists(pathHome)) {
            GameStateManager.currentSaveType = SaveType.NEWGAME;
        } else {

            // ACTIVE RUN
            if (File.Exists(pathPlayer) && File.Exists(pathMap)) {
                GameStateManager.currentSaveType = SaveType.ACTIVERUN;
            } 
            // SAVED HOME — NO ACTIVE RUN
            else {
                GameStateManager.currentSaveType = SaveType.SAVEDHOME;
            }
        }

        if (!virtualCamera) {
            GameObject camObject = GameObject.FindGameObjectWithTag("VirtualCamera");
            virtualCamera = camObject.GetComponent<CinemachineVirtualCamera>();

            if (!bedroomPixelPerf) {
                bedroomPixelPerf = camObject.GetComponent<CinemachinePixelPerfect>();
                Debug.LogWarning("CinemachinePixelPerfect component null! Reassigned.");
            }

            Debug.LogWarning("VirtualCamera component null! Reassigned.");
        }

        if (!pixelPerfectCamera) {
            pixelPerfectCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<PixelPerfectCamera>();
            Debug.LogWarning("PixelPerfectCamera component or main camera null! Reassigned.");
        }

        // If the HOME scene is loaded after a player death, don't show the main menu
        if (GameStateManager.GetState() == GAMESTATE.GAMEOVER) {
            playerCont.gameObject.transform.position = livingRoomPoint.transform.position;
            montagePlaying = false;
            mainMenuScreen.SetActive(false);
            TransitionManager.EndLeaf(true);

            SetupCamera(false);
            
            StartHome();
        } 
        // Otherwise, show main menu
        else {
            mainMenuScreen.SetActive(true);
            GameStateManager.SetStage(0);
            GameStateManager.SetLevel(0);
            GameStateManager.SetState(GAMESTATE.MAINMENU);

            montagePlaying = true;
            stopMontage = false;
            playerInPos = false;
            readyToPlay = false;

            SetupCamera(true);
        }
    }

    private void Update() {

        if (montagePlaying) {
            Cooldown();
        }

        // If montage animation has concluded, and player has pressed an active save slot—
        if (readyToPlay && playerInPos) {

            // Reset flags
            readyToPlay = false;
            playerInPos = false;

            // NO SAVE SLOT (NEW GAME)
            if (GameStateManager.currentSaveType == SaveType.NEWGAME) {

                GameStateManager.tutorialEnabled = true;

                // Reset saved profile stats if there is no profile
                GameStateManager.homeManager.ResetHome();

                GameStateManager.SetSave(false);

                MainMenuTransition(true);
                Debug.LogWarning("CUTSCENE");
            } 
            // SAVE SLOT
            else {

                // Load save slot
                GameStateManager.SetSave(false);

                GameStateManager.homeManager.LoadHome();

                MainMenuTransition(false);
                Debug.LogWarning("WENT TO SAVED HOME");
            }
        }
    }

    private void Cooldown() {
        if (playerCont.contactColl.isActiveAndEnabled) {
            playerCont.contactColl.enabled = false;
        }

        montageTimer += Time.deltaTime;
        
        if (montageTimer > montageSpeed) {
            if (!stopMontage) {
                montageTimer = 0;

                // Switch player position
                playerCont.gameObject.transform.position = montagePoints[montageIndex].transform.position;

                // Switch Fallow position


                if (montageIndex + 1 > montagePoints.Count - 1) {
                    montageIndex = 0;
                } else {
                    montageIndex++;
                }

            } else {
                montageTimer = 0;
                montagePlaying = false;

                // Switch player position
                playerCont.gameObject.transform.position = playPoint.transform.position;
                playerCont.contactColl.enabled = true;

                // Switch Fallow position
                
                StartCoroutine(PauseAfterPlayerInPos());
            }
        }
    }

    // Pause for a split second after player is ready to start the game
    private IEnumerator PauseAfterPlayerInPos() {

        yield return new WaitForSeconds(0.5f);

        stopMontage = false;
        playerInPos = true;
    }

    private void SetupCamera(bool bedroom) {

        switch (bedroom) {

            // Setup camera for bedroom zoom-out transition
            case true:
                cameraState = CameraState.BEDROOM;

                homeLookAt.bedroom = true;

                bedroomPixelPerf.enabled = false;
                pixelPerfectCamera.enabled = false;

                virtualCamera.m_Lens.OrthographicSize = transitionSize;

                break;
            // Setup camera for player death respawn
            case false:
                cameraState = CameraState.DEFAULT;

                homeLookAt.bedroom = false;
                homeLookAt.room4 = false;
                homeLookAt.room2 = true;

                bedroomPixelPerf.enabled = false;
                pixelPerfectCamera.enabled = false;

                virtualCamera.m_Lens.OrthographicSize = defaultSize;

                bedroomPixelPerf.enabled = true;
                pixelPerfectCamera.enabled = true;

                break;
        }
    }

    private IEnumerator BedroomTransition() {

        float ortho = virtualCamera.m_Lens.OrthographicSize;

        bedroomPixelPerf.enabled = false;
        pixelPerfectCamera.enabled = false;

        homeLookAt.bedroom = false;
        homeLookAt.room4 = true;

        while (ortho < defaultSize) {
            ortho += 0.025f;
            virtualCamera.m_Lens.OrthographicSize = ortho;
            yield return new WaitForSeconds(0.06f);
        }

        bedroomPixelPerf.enabled = true;
        pixelPerfectCamera.enabled = true;

        mainMenuScreen.SetActive(false);

        StartHome();
    }

    // Called AFTER scene is loaded and main menu visibility checks have completed
    public void StartHome() {

        // Hides the cursor
        Cursor.visible = false;

        // Updates gamestate to PLAYING
        GameStateManager.SetState(GAMESTATE.PLAYING);

        // Enables player functions if not null
        if (playerCont != null) {
            playerCont.PlayerStart(true);
        }

        // Subscribes the SaveHome() function to the OnNewPickup event
        PlayerController.EOnNewPickup += GameStateManager.homeManager.SaveHome;

        // Saves the home
        GameStateManager.homeManager.SaveHome();
    }

    public void PlayButton() {

        // Player does NOT have an existing save slot (START NEW GAME + CUTSCENE)
        if (GameStateManager.currentSaveType == SaveType.NEWGAME) {
            stopMontage = true;
            readyToPlay = true;
        }
        // Player has an existing save slot
        else {
            GameStateManager.tutorialEnabled = false;
            saveSlots.SetActive(true);
        }
    }

    public void SaveSlotButton() {

        // Player has an active run in progress
        if (GameStateManager.currentSaveType == SaveType.ACTIVERUN) {

            // Load home data for tutorial status
            GameStateManager.homeManager.LoadHome();

            // Load save data to get level number
            MapData data = SaveSystem.LoadMap();

            GameStateManager.SetLevel(data.levelNum);
            GameStateManager.SetStage(data.stageNum);

            GameStateManager.SetSave(true);

            // Load level
            TransitionManager.StartLeaf(GameStateManager.GetStage() + sceneInfo.sceneOffset);
        }
        // Player does NOT have an active run in progress
        else {
            stopMontage = true;
            readyToPlay = true;

            mainMenuScreen.SetActive(false);
        }
    }

    public void MainMenuTransition(bool tutorialStatus) {

        switch (tutorialStatus) {
            case true:
                Debug.Log("Started tutorial transition!");
                // TODO: Implement beginning cutscene

                StartCoroutine(BedroomTransition());

                break;
            case false:
                Debug.Log("Started main menu transition!");

                virtualCamera.m_Lens.OrthographicSize = defaultSize;
                homeLookAt.bedroom = false;
                homeLookAt.room4 = true;
                pixelPerfectCamera.enabled = true;
                bedroomPixelPerf.enabled = true;
                
                StartHome();
                break;
        }
    }

    public void QuitButton() {
        Debug.Log("QUIT!");
        Application.Quit();
    }
}
