﻿using MyBox;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable; // to use with Photon's CustomProperties

public class GameManager : MonoBehaviourPunCallbacks, IPunObservable
{
    #region Public fields

    // Don't leave the m_levelType as None, set it to a value as soon as possible (when making new scenes)
    public enum LevelTypes
    {
        None,
        Level,
        Lobby,
        Narrative,
        BossLevel,
        PVP
    }

    // C# properties for accessing the private variables
    public LevelTypes TypeOfLevel { get { return m_levelType; } private set { m_levelType = value; } }
    public bool IsLevel { get { return !(m_levelType == LevelTypes.Lobby || m_levelType == LevelTypes.Narrative); } }

    public int PlayersReady { get { return m_playersReady; } private set { m_playersReady = value; } }
    // num players needed to be ready can be fetched via PhotonNetwork.CurrentRoom.PlayerCount
    public int OrbsNeededToReady { get { return m_OrbsNeededToStartGame; } private set { m_OrbsNeededToStartGame = value; } }
    public float PaintPercentageNeededToWin { get { return m_paintPercentageNeededToWin; } private set { m_paintPercentageNeededToWin = value; } }
    public float CurrentPaintPercentage { get { return m_paintProgress; } private set { m_paintProgress = value; } }

    // Room custom properties
    public const string RedOrbKey = "RedLoanedToPhotonID";
    public const string OrangeOrbKey = "OrangeLoanedToPhotonID";
    public const string YellowOrbKey = "YellowLoanedToPhotonID";
    public const string GreenOrbKey = "GreenLoanedToPhotonID";
    public const string BlueOrbKey = "BlueLoanedToPhotonID";
    public const string VioletOrbKey = "VioletLoanedToPhotonID";
    public const string BrownOrbKey = "BrownLoanedToPhotonID";
    public const string QuicksilverOrbKey = "QuicksilverLoanedToPhotonID";
    public const string IndigoOrbKey = "IndigoLoanedToPhotonID";
    public const string OrbsNeededKey = "OrbsNeededToPhotonID";

    // Player custom properties

    //) lobby or narrative levels
    public const string IsPlayerReady = "Ready";

    //) lobby levels
    public const string OrbOwnedInLobbyKey1 = "Orb1Owned";
    public const string OrbOwnedInLobbyKey2 = "Orb2Owned";

    //) "level"-type levels
    public const string PlayerAliveKey = "IsPlayerAlive";

    //) PVP levels
    public const string PlayerRemaining = "PlayersLeft";

    // Name of scenes
    public const string LobbySceneName = "Starting Level";
    public const string WinSceneName = "YouWinScene";
    public const string OfficeLv1Name = "Office Level 1";
    public const string CutsceneName = "Office Boss Cutscene";
    #endregion

    #region Private Fields

    [Separator("Player properties")]
    [Tooltip("If playerSpawnpoint is unassigned, spawn using these default coordinates")]
    [SerializeField]
    private Vector3 m_defaultSpawn = new Vector3(-8, 1.5f, 4);

    [Tooltip("Players will spawn on these location(s)")]
    [SerializeField]
    private GameObject[] m_playerSpawnpoints;

    [Tooltip("The prefab to use for representing the player")]
    [SerializeField]
    private GameObject m_playerPrefab;

    private uint m_currentSpawnIndex = 0; // index of the current spawn to spawn the player, used if m_playerSpawnpoints exists

    [Separator("General level properties")]
    [SerializeField]
    private LevelTypes m_levelType = LevelTypes.None;

    public bool IsLoadingNewScene
    {
        get { return m_isLoadingNewScene; }
    }
    private bool m_isLoadingNewScene = false;

    [Separator("Lobby level properties")]
    private int m_playersReady = 0;

    [SerializeField]
    private int m_OrbsNeededToStartGame = 2;

    private GameObject m_cameraPrefab; // Instantiate a camera prefab once a player returns back from a level

    // Confiscate player orbs var
    private bool resetPlayerOrbs = false; // this flag tells us whenever a player lost and returns to the lobby, confiscate their orbs!

    [Separator("Lobby / narrative level properties")]
    [SerializeField]
    private string m_levelAfterReadyUp = OfficeLv1Name;

    [Separator("Normal gameplay level properties")]
    [SerializeField]
    private string m_levelAfterBeatingStage = WinSceneName;

    // Painting variables
    [Range(0, 1)]
    [SerializeField]
    private float m_paintPercentageNeededToWin = 0.75f;

    private float m_paintProgress = 0f;

    //[Separator("Boss level properties")]
    //[SerializeField]
    private int enemiesRemaining = 0;

    #endregion

    #region Dialogue system fields

    [Separator("GUI references")]
    public GameObject popUpBox;

    private Button m_leaveButton; // the reference to the leave button in canvas

    Animator animator;
    TMPro.TMP_Text popUpFullText;
    TMPro.TMP_Text popUpImageText;
    Image dialogueHalfImage;
    Image dialogueFullImage;
    GameObject nextButton;
    GameObject acceptButton;

    Sprite[] dialogueImages;
    string[] dialogueMessages;

    bool WindowOpen = false;
    bool PodiumMessage = false;

    Orb m_currentOrb;
    OrbPodium m_orbPodium; // keep track of current podium so we can close access to orb after player has obtained it
    OrbManager m_playerOrbManager;

    string m_currentItem;
    ItemPodium m_itemPodium;
    ItemManager m_playerItemManager;
    int dialoguePage = 0;

    #endregion

    #region Components

    [Separator("References to other components")]
    [SerializeField]
    private GameObject m_enemyManagerObject;
    [SerializeField]
    private GameObject m_paintingManagerObject;
    [SerializeField]
    private GameObject m_orbValueManagerObject;

    private EnemyManager m_enemManager;
    private PaintingManager m_paintManager;

    #endregion

    #region MonoBehaviour callbacks

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "Office Level 1" || SceneManager.GetActiveScene().name == "Office Level 2")
        {
            AudioScript audioScript = GameObject.FindGameObjectWithTag("SongAudio").GetComponent<AudioScript>();
            if (audioScript)
                audioScript.PlaySong(AudioScript.SongType.STAGE);
        }

        if (SceneManager.GetActiveScene().name == "Office Boss Cutscene" || SceneManager.GetActiveScene().name == "Office Level 3" ||
            SceneManager.GetActiveScene().name == "PlayerPVPScene")
        {
            AudioScript audioScript = GameObject.FindGameObjectWithTag("SongAudio").GetComponent<AudioScript>();
            if (audioScript)
                audioScript.PlaySong(AudioScript.SongType.BOSS);
        }

        if ((SceneManager.GetActiveScene().name == WinSceneName) || !PhotonNetwork.InRoom)
        {
            // don't run the rest of the start statement, return immediately
            return;
        }

        if (m_playerPrefab == null)
        {
            Debug.LogError("<Color=Red>Missing player prefab");
        }
        else
        {
            m_cameraPrefab = Resources.Load<GameObject>("Main Camera"); // load the camera resource before we might need it

            if (m_levelType == LevelTypes.Narrative)
            {
                // always setup a custom properties for all player
                PhotonHashtable properties = new PhotonHashtable
                {
                    {IsPlayerReady, false }
                };

                // This keep tracks of whether a player is ready or not
                // so that if the player was ready, and left, it would
                // decrement the amount of players ready
                PhotonNetwork.LocalPlayer.SetCustomProperties(properties);

                // Checking if we're in the cutscene
                if (SceneManager.GetActiveScene().name == CutsceneName)
                {
                    //Debug.Log("We're in the cutscene!");
                    GameObject playerObject = PhotonNetwork.LocalPlayer.TagObject as GameObject;
                    //if (playerObject && playerObject.transform.Find("PlayerCamera"))
                    //{
                    //    playerObject.transform.Find("PlayerCamera").gameObject.SetActive(false); // disable player cam so that cutscene camera works
                    //}
                    playerObject.GetComponentInChildren<Camera>().enabled = false; // disable player cam so that cutscene camera works
                }
            }
            else if (m_levelType == LevelTypes.Lobby)
            {
                // Initalize the Room's custom properties once for the starting level
                // be sure to clear these properties when moving to the first level

                PhotonHashtable properties;
                if (PhotonNetwork.IsMasterClient)
                {
                    // only initialize the Room custom properties once upon joining new scene

                    int[] array2size = new int[] { -1, -1 }; // first element, ID that is browsing the orb, second element, ID that has obtained the orb

                    // Essentially we're keeping track of who has which orb in the lobby
                    // we don't need to store a variable and sync the variable, rather
                    // we're delegating that task to Photon via CustomProperties (basically a HashTable)
                    properties = new PhotonHashtable
                    {
                        {RedOrbKey, array2size},
                        {OrangeOrbKey, array2size},
                        {YellowOrbKey, array2size},
                        {GreenOrbKey, array2size},
                        {BlueOrbKey, array2size},
                        {VioletOrbKey, array2size},
                        {BrownOrbKey, array2size},
                        {QuicksilverOrbKey, array2size},
                        {IndigoOrbKey, array2size},
                        {OrbsNeededKey, m_OrbsNeededToStartGame}
                    };

                    PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
                }

                // always setup a custom properties for all player
                properties = new PhotonHashtable
                {
                    {OrbOwnedInLobbyKey1, OrbPodium.OrbTypes.None},
                    {OrbOwnedInLobbyKey2, OrbPodium.OrbTypes.None},
                    {IsPlayerReady, false }
                };

                // please don't let there be another orb type owned, this is somewhat tedious to scale (2 is tedious enough)

                PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
                // these custom properties will be mutated whenver a player picks up a lobby orb / return a lobby orb
            }
            else if (m_levelType == LevelTypes.Level || m_levelType == LevelTypes.BossLevel || m_levelType == LevelTypes.PVP)
            {
                // Keep track of whether a player is still alive or not
                PhotonHashtable properties = new PhotonHashtable
                {
                    {PlayerAliveKey, true},
                };
                PhotonNetwork.LocalPlayer.SetCustomProperties(properties);

                if (m_levelType == LevelTypes.PVP && PhotonNetwork.IsMasterClient)
                {
                    // master client needs to keep track of the number of players alive, game will end for last one standing (no ties possible I don't want to handle that edge case)

                    // see the lobby PhotonHashTable thing to see how to keep track of players alive
                    PhotonHashtable roomProperty = new PhotonHashtable
                    {
                        {PlayerRemaining, PhotonNetwork.PlayerList.Length}
                    };

                    PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperty);
                }
            }

            // Add in Painting Manager and Orb Value Manager classes manually
            GameObject cacheObj;

            if (!GameObject.Find("OrbValueManager"))
            {
                Instantiate(m_orbValueManagerObject);
            }
            
            // Add painting manager and enemy manager if it is a normal level
            if (m_levelType == LevelTypes.Level)
            {
                if (!(cacheObj = GameObject.Find("PaintingManager")))
                {
                    GameObject obj = Instantiate(m_paintingManagerObject);
                    m_paintManager = obj.GetComponent<PaintingManager>();
                }
                else
                {
                    m_paintManager = cacheObj.GetComponent<PaintingManager>();
                }

                if (!(cacheObj = GameObject.Find("EnemyManager")))
                {
                    GameObject obj = Instantiate(m_enemyManagerObject);
                    m_enemManager = obj.GetComponent<EnemyManager>();
                }
                else
                {
                    m_enemManager = cacheObj.GetComponent<EnemyManager>();
                }
            }

            if (PlayerController.LocalPlayerInstance == null)
            {
                if (HealthScript.LocalPlayerInstance == null)
                {
                    SpawnEntirelyNewPlayerAtSpawnpoint();
                }
                else
                {
                    //Debug.LogFormat("Ignoring scene load for {0}", SceneManagerHelper.ActiveSceneName);

                    // This portion of the code is reached whenever HealthScript.LocalPlayerInstance exists
                    // meaning we're loading a new scene (player is Don't Destroy on load)

                    GameObject playerObject = PhotonNetwork.LocalPlayer.TagObject as GameObject;

                    if (!(m_levelType==LevelTypes.Level || m_levelType==LevelTypes.BossLevel || m_levelType == LevelTypes.PVP) && playerObject)
                    {
                        object playerAliveProperty;
                        bool spawnNewPlayerInstance = false;

                        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerAliveKey, out playerAliveProperty))
                        {
                            if (!(bool)playerAliveProperty)
                            {
                                // the player is dead, remove any existing gameObject and spawn a new one

                                spawnNewPlayerInstance = true;

                                TidyUpBeforeStartingNewLevel();

                                // We need to instantiate a new main camera 
                                // because we've destroyed our previous one and the lobby doesn't have it anymore
                                Instantiate(m_cameraPrefab);

                                PhotonNetwork.Destroy(PhotonView.Get(playerObject));

                                SpawnEntirelyNewPlayerAtSpawnpoint();
                            }
                        }

                        if (!spawnNewPlayerInstance)
                        {
                            TransitionPlayerToNewRoom();
                        }
                    }
                    else
                    {
                        TransitionPlayerToNewRoom();
                    }
                }
            }
            else
            {
                //Debug.LogFormat("Ignoring scene load for {0}", SceneManagerHelper.ActiveSceneName);
            }
        }

        SetPopUpVariables();

        // Find reference to the leave button in Canvas
        GameObject canvas;
        if (canvas = GameObject.Find("Canvas"))
        {
            m_leaveButton = canvas.transform.Find("LevelUI").transform.Find("TopPanel").transform.Find("LeaveButton").GetComponent<Button>();
        }
    }

    private void Update()
    {
        // Only confiscate player orbs for all players if players have just returned from a level and are all dead
        if (SceneManager.GetActiveScene().name == LobbySceneName && !resetPlayerOrbs)
        {
            resetPlayerOrbs = true;

            GameObject playerObj = PhotonNetwork.LocalPlayer.TagObject as GameObject;

            if (playerObj)
            {
                object playerAliveProperty;

                if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PlayerAliveKey, out playerAliveProperty))
                {
                    if (!(bool)playerAliveProperty)
                    {
                        // the player is dead, remove all of their orbs
                        playerObj.GetComponent<OrbManager>().ResetOrbs();
                    }
                }
            }
            else
            {
                // playerObj not ready to check yet
                resetPlayerOrbs = false;
            }
        }

        if (PhotonNetwork.IsMasterClient)
        {
            if (m_levelType == LevelTypes.Lobby || m_levelType == LevelTypes.Narrative)
            {
                // check if all players are ready
                if (!m_isLoadingNewScene && m_playersReady >= PhotonNetwork.CurrentRoom.PlayerCount)
                {
                    LoadLevel(m_levelAfterReadyUp);
                }
            }
            else if (m_levelType != LevelTypes.None)
            {
                if (m_levelType == LevelTypes.Level)
                {            
                    m_paintProgress = PaintingManager.paintingProgress();
                    //print(m_paintProgress);

                    if (!m_isLoadingNewScene && m_paintProgress > m_paintPercentageNeededToWin)
                    {
                        //TODO: add in loot acquisition here
                        LoadLevel(m_levelAfterBeatingStage);
                    }
                }

                //Boss win condition
                if (m_levelType == LevelTypes.BossLevel)
                {
                    if (!m_isLoadingNewScene && enemiesRemaining <= 0)
                    {
                        LoadLevel(m_levelAfterBeatingStage);
                    }
                }

                // PVP
                if (m_levelType == LevelTypes.PVP)
                {
                    // Check if there is one player still remaining
                    PhotonHashtable roomProperty = PhotonNetwork.CurrentRoom.CustomProperties;
                    object playerRemainingProperty;
                    if (!m_isLoadingNewScene && roomProperty.TryGetValue(PlayerRemaining, out playerRemainingProperty) && (int)playerRemainingProperty <= 1)
                    {
                        // somehow save the name for next stage
                        string nameOfWinner = "Nobody";

                        foreach (var player in PhotonNetwork.PlayerList)
                        {
                            object playerAliveProperty;

                            if (player.CustomProperties.TryGetValue(PlayerAliveKey, out playerAliveProperty))
                            {
                                if ((bool)playerAliveProperty)
                                {
                                    nameOfWinner = player.NickName; // if this player is still alive, this player is the last remaining
                                    break;
                                }
                            }
                        }

                        photonView.RPC("ChoosePVPWinner", RpcTarget.All, nameOfWinner);

                        LoadLevel(m_levelAfterBeatingStage);
                    }
                }


                if (!m_isLoadingNewScene)
                {
                    bool isAnyPlayerAlive = false;

                    foreach (Player p in PhotonNetwork.PlayerList)
                    {
                        object playerAliveProperty;
                        if (p.CustomProperties.TryGetValue(PlayerAliveKey, out playerAliveProperty))
                        {
                            if ((bool)playerAliveProperty)
                            {
                                isAnyPlayerAlive = true;
                            }
                        }
                        else
                        {
                            // race condition (need to load properties), players are still loading in
                            isAnyPlayerAlive = true;
                        }
                    }

                    if (!isAnyPlayerAlive)
                    {
                        LoadLevel(LobbySceneName);
                    }
                }
            }
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Map an Orb to a GameManager.[Orb]key string in order to utilize both room and player CustomProperties.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private string FetchOrbKey(Orb.Element type)
    {
        switch (type)
        {
            case Orb.Element.Wrath:
                return RedOrbKey;
            case Orb.Element.Fire:
                return OrangeOrbKey;
            case Orb.Element.Light:
                return YellowOrbKey;
            case Orb.Element.Nature:
                return GreenOrbKey;
            case Orb.Element.Water:
                return BlueOrbKey;
            case Orb.Element.Poison:
                return VioletOrbKey;
            case Orb.Element.Earth:
                return BrownOrbKey;
            case Orb.Element.Wind:
                return QuicksilverOrbKey;
            case Orb.Element.Darkness:
                return IndigoOrbKey;
            default:
                return "InvalidKey";
        }
    }

    private void SetPopUpVariables()
    {
        animator = popUpBox.GetComponent<Animator>();
        popUpFullText = popUpBox.transform.Find("FullText").GetComponent<TMPro.TMP_Text>();
        popUpImageText = popUpBox.transform.Find("ImageText").GetComponent<TMPro.TMP_Text>();
        dialogueHalfImage = popUpBox.transform.Find("HalfImage").GetComponent<Image>();
        dialogueFullImage = popUpBox.transform.Find("FullImage").GetComponent<Image>();
        nextButton = popUpBox.transform.Find("NextButton").gameObject;
        acceptButton = popUpBox.transform.Find("AcceptButton").gameObject;
        acceptButton.SetActive(false);
    }

    /// <summary>
    /// Create a new instance for the player and spawns them at a spawnpoint.
    /// </summary>
    private void SpawnEntirelyNewPlayerAtSpawnpoint()
    {
        // Determine the spawnpoint to spawn the player on for the first time
        m_currentSpawnIndex = (uint)(m_playerSpawnpoints.Length > 0 ? (PhotonNetwork.LocalPlayer.ActorNumber % m_playerSpawnpoints.Length) - 1 : 0);
        Quaternion spawnRotation = Quaternion.identity;
        Vector3 spawnPosition = ReturnSpawnpointPosition(ref spawnRotation);
        photonView.RPC("SpawnPlayer", PhotonNetwork.LocalPlayer, spawnPosition, spawnRotation);
    }

    [PunRPC]
    private void SpawnPlayer(Vector3 spawnPos, Quaternion spawnRot)
    {
        PhotonNetwork.Instantiate("Player/"+m_playerPrefab.name, spawnPos, spawnRot);
    }

    [PunRPC]
    private void ReadyUp()
    {
        m_playersReady++;
    }

    [PunRPC]
    private void UnReady()
    {
        m_playersReady--;
    }

    [PunRPC]
    private void AddEnemy()
    {
        enemiesRemaining += 1;
    }

    [PunRPC]
    private void RemoveEnemy()
    {
        enemiesRemaining -= 1;
    }

    /// <summary>
    /// Clean up everything as we load the character into the new level.
    /// </summary>
    private void TidyUpBeforeStartingNewLevel()
    {
        // Clear the custom properties as we transition to a new scene
        PhotonNetwork.CurrentRoom.CustomProperties.Clear(); // clear room's custom properties (will be called more than once)
        PhotonNetwork.LocalPlayer.CustomProperties.Clear(); // clear player's custom properties

        // Destroy the current camera because the player already has one
        Destroy(GameObject.Find("Main Camera"));
    }

    /// <summary>
    /// Given that the Player's TabObject (or instance) already exist, just reset the references
    /// to GUI and stuff, and set players at the level's spawnpoints so that we won't have to spawn
    /// an entirely new player instance
    /// </summary>
    private void TransitionPlayerToNewRoom()
    {
        PhotonView playerView = PhotonView.Get(HealthScript.LocalPlayerInstance);
        GameManager newLevelGameManager = GameObject.Find("GameManager").GetComponent<GameManager>();

        TidyUpBeforeStartingNewLevel();

        if (playerView.IsMine)
        {
            // Teleport only all players to the first spawn? (bug)
            playerView.RPC("RespawnPlayer", PhotonNetwork.LocalPlayer);

            GameObject player = playerView.gameObject;

            // IMPORTANT: reset the references to gui b/c old references won't work after scene load

            // Reset their health GUI
            player.GetComponent<SpawnGUI>().ResetUIAfterSceneLoad();

            // Reset their spell manager GUI reference
            player.GetComponent<SpellManager>().Initialization();

            // Reset their spell GUI reference
            player.GetComponent<OrbManager>().Initialization();

            // Set the character's speed depending on the level
            player.GetComponent<PlayerMovement>().SetSpeedDependingOnLevel(newLevelGameManager.IsLevel);
        }

        m_paintProgress = 0f; // reset paint progress
    }

    #endregion

    #region Public Methods

    public void AddCurrentOrb()
    {
        if (m_currentOrb != null)
        {
            m_playerOrbManager.AddSpellOrb(m_currentOrb, true);

            // Update custom properties
            string orbKey = FetchOrbKey(m_currentOrb.getElement());

            // fetch, alter, then set room custom properties
            PhotonHashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;
            int[] orbProperties = (int[])roomProperties[orbKey];
            roomProperties[orbKey] = new int[]{orbProperties[0],PhotonNetwork.LocalPlayer.ActorNumber};
            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

            // fetch, alter, then set player custom properties
            PhotonHashtable playerProperties = PhotonNetwork.LocalPlayer.CustomProperties;

            // Check if the orb is empty, if it is set it, otherwise set it to the second key
            if ((OrbPodium.OrbTypes)playerProperties[OrbOwnedInLobbyKey1] == OrbPodium.OrbTypes.None)
            {
                playerProperties[OrbOwnedInLobbyKey1] = OrbPodium.FetchOrbType(orbKey);
            }
            else
            {
                playerProperties[OrbOwnedInLobbyKey2] = OrbPodium.FetchOrbType(orbKey);
            }
            
            PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);

            CloseWindow();
        }
    }
    
    public void AddCurrentItem()
    {
        if (m_currentItem != null)
        {
            m_playerItemManager.RPCAddItem(m_currentItem);
            //m_playerItemManager.AddItem(m_currentItem);

            CloseWindow();
        }
    }

    /// <summary>
    /// Alters the accept button behaviour in the Popupdialogue canvas object 
    /// </summary>
    public void ChangeGUIMode(AcceptButtonHandler.AcceptMode newMode)
    {
        acceptButton.GetComponent<AcceptButtonHandler>().ChangeCurrentMode(newMode);
    }

    /// <summary>
    /// Wrapper function to call CloseWindowVisually() and currentPodium.CloseWindow()
    /// </summary>
    public void CloseWindow()
    {
        CloseWindowVisually();
        if (m_orbPodium)
            m_orbPodium.CloseWindow();
    }

    public void CloseWindowVisually()
    {
        if (WindowOpen)
        {
            animator.SetTrigger("close");
            WindowOpen = false;
            m_playerOrbManager = null;
            m_playerItemManager = null;
            m_currentOrb = null;
            acceptButton.SetActive(false);
            nextButton.SetActive(true);
        }
    }

    [PunRPC]
    public void ChoosePVPWinner(string nameOfWinner)
    {
        SceneDataSharer.PVPWinner = nameOfWinner; // update the static var so that the winner of the PVP duel will be correctly displayed in the win scene
    }

    /// <summary>
    /// Invoked by the Canvas button "Leave Room"
    /// </summary>
    public void LeaveRoom()
    {
        OrbManager.orbHistory.Clear(); // don't retain memory of spells after leaving game

        if (TypeOfLevel == LevelTypes.None)
        {
            SceneDataSharer.PVPWinner = ""; // reset static vars
        }

        AudioScript audioScript = GameObject.FindGameObjectWithTag("SongAudio").GetComponent<AudioScript>();
        audioScript.PlaySong(AudioScript.SongType.LOBBY);

        // Leave the room and disconnect
        PhotonNetwork.LeaveRoom();
        PhotonNetwork.Disconnect();
    }

    /// <summary>
    /// Load a new level and transition everyone from the current scene to the new level immediately.
    /// Guarentees that the level will only be loaded once if this function is invoked more than once.
    /// </summary>
    /// <param name="nameOfScene">The name of the scene to load</param>
    public void LoadLevel(string nameOfScene)
    {
        if (!m_isLoadingNewScene)
        {
            m_isLoadingNewScene = true;

            // disable leave button (this will mess things if players decide to leave during this period)
            if (m_leaveButton)
            {
                m_leaveButton.interactable = false;
            }

            if (SceneManager.GetActiveScene().name == CutsceneName)
            {
                //Debug.Log("We're leaving the cutscene!");
                GameObject playerObject = PhotonNetwork.LocalPlayer.TagObject as GameObject;
                //playerObject.transform.Find("PlayerCamera").gameObject.SetActive(true); // re-enable camera
                playerObject.GetComponentInChildren<Camera>().enabled = true; // re-enable camera
            }

            PhotonNetwork.LoadLevel(nameOfScene);        
        }
    }


    public void NextPage()
    {
        dialoguePage++;
        SetPage();
    }

    public void PopUpOrb(string[] messages, Sprite[] images, Orb orbType, OrbManager orbManager, OrbPodium orbPodiumScript)
    {
        if (!WindowOpen)
        {
            m_orbPodium = orbPodiumScript;
            m_currentOrb = orbType;
            m_playerOrbManager = orbManager;

            dialogueMessages = messages;
            dialogueImages = images;
            dialoguePage = 0;

            animator.SetTrigger("pop");
            PodiumMessage = true;
            WindowOpen = true;

            SetPage();
        }
    }

    public void PopUp(string[] messages, Sprite[] images)
    {
        if (!WindowOpen)
        {
            dialogueMessages = messages;
            dialogueImages = images;
            dialoguePage = 0;

            animator.SetTrigger("pop");
            PodiumMessage = false;
            WindowOpen = true;

            SetPage();
        }
    }

    public void PopUpItem(string[] messages, Sprite[] images, string itemName, ItemManager itemManager, ItemPodium itemPodium)
    {
        if (!WindowOpen)
        {
            m_itemPodium = itemPodium;
            m_currentItem = itemName;
            m_playerItemManager = itemManager;

            dialogueMessages = messages;
            dialogueImages = images;
            dialoguePage = 0;

            animator.SetTrigger("pop");
            PodiumMessage = true;
            WindowOpen = true;

            SetPage();
        }
    }

    public void RemoveCurrentOrb()
    {
        if (m_currentOrb != null)
        {
            m_playerOrbManager.RemoveSpellOrb(m_currentOrb, true);

            // Update custom properties
            string orbKey = FetchOrbKey(m_currentOrb.getElement()); // for the room
            OrbPodium.OrbTypes orbType = OrbPodium.FetchOrbType(orbKey);

            // fetch, alter, then set room custom properties
            PhotonHashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;
            int[] orbProperties = (int[])roomProperties[orbKey];
            roomProperties[orbKey] = new int[] { orbProperties[0], -1 };
            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

            // fetch, alter, then set player custom properties
            PhotonHashtable playerProperties = PhotonNetwork.LocalPlayer.CustomProperties;

            // Check if the first slot is holding the the orb, if it is, clear it
            // otherwise clear the second slot
            
            if ((OrbPodium.OrbTypes)playerProperties[OrbOwnedInLobbyKey1] == orbType)
            {
                playerProperties[OrbOwnedInLobbyKey1] = OrbPodium.OrbTypes.None;
            }
            else
            {
                playerProperties[OrbOwnedInLobbyKey2] = OrbPodium.OrbTypes.None;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);

            CloseWindowVisually();
            m_orbPodium.CloseWindow();
        }
    }

    /// <summary>
    /// Get the position of the spawnpoint (if it exists), otherwise return default
    /// </summary>
    /// <param name="spawnRotation">The rotation of the spawn, pass a quaternion by reference and it will be updated with this variable.</param>
    /// <returns>The position of the spawnpoint</returns>
    public Vector3 ReturnSpawnpointPosition(ref Quaternion spawnRotation)
    {
        if (m_playerSpawnpoints.Length > 0 && m_currentSpawnIndex < PhotonNetwork.PlayerList.Length)
        {
            Vector3 spawnPosition = m_playerSpawnpoints[m_currentSpawnIndex].transform.position;
            spawnRotation = m_playerSpawnpoints[m_currentSpawnIndex].transform.rotation;
            return spawnPosition;
        }
        else
        {
            return m_defaultSpawn;
        }
    }

    /// <summary>
    /// Player readies up. Used for the lobby.
    /// </summary>
    public void RPCReadyUp()
    {
        photonView.RPC("ReadyUp", RpcTarget.All);
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.IsLocal)
        {
            // update the custom property for local player (they're ready)
            PhotonHashtable localPlayerProperties = PhotonNetwork.LocalPlayer.CustomProperties;
            localPlayerProperties[IsPlayerReady] = true;
            PhotonNetwork.LocalPlayer.SetCustomProperties(localPlayerProperties);
        }
    }

    /// <summary>
    /// Player unreadies. Used for the lobby.
    /// </summary>
    public void RPCUnready()
    {
        photonView.RPC("UnReady", RpcTarget.All);
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.IsLocal)
        {
            // update the custom property for local player (they're no longer ready)
            PhotonHashtable localPlayerProperties = PhotonNetwork.LocalPlayer.CustomProperties;
            localPlayerProperties[IsPlayerReady] = false;
            PhotonNetwork.LocalPlayer.SetCustomProperties(localPlayerProperties);
        }
    }

    public void RPCAddEnemy()
    {
        photonView.RPC("AddEnemy", RpcTarget.All);
    }

    public void RPCRemoveEnemy()
    {
        photonView.RPC("RemoveEnemy", RpcTarget.All);
    }

    public void SetFullImage()
    {
        popUpFullText.gameObject.SetActive(false);
        dialogueFullImage.gameObject.SetActive(true);
        dialogueHalfImage.gameObject.SetActive(false);
        popUpImageText.gameObject.SetActive(false);

        dialogueFullImage.sprite = dialogueImages[dialoguePage];
    }

    public void SetFullText()
    {
        popUpFullText.gameObject.SetActive(true);
        dialogueHalfImage.gameObject.SetActive(false);
        dialogueFullImage.gameObject.SetActive(false);
        popUpImageText.gameObject.SetActive(false);

        popUpFullText.text = dialogueMessages[dialoguePage];
    }

    public void SetImageText()
    {
        popUpFullText.gameObject.SetActive(false);
        dialogueHalfImage.gameObject.SetActive(true);
        popUpImageText.gameObject.SetActive(true);
        dialogueFullImage.gameObject.SetActive(false);

        dialogueHalfImage.sprite = dialogueImages[dialoguePage];
        popUpImageText.text = dialogueMessages[dialoguePage];
    }

    public void SetPage()
    {
        if (dialogueImages.Length <= dialoguePage || dialogueImages[dialoguePage] == null)
        {
            //No Images
            if (dialogueMessages.Length <= dialoguePage || dialogueMessages[dialoguePage] == "")
            {
                //No Messages
                Debug.LogError("Went past given messages/images");
            }
            else
            {
                //Messages
                SetFullText();
            }
        }
        else
        {
            //Images
            if (dialogueMessages.Length <= dialoguePage || dialogueMessages[dialoguePage] == "")
            {
                //No Messages
                SetFullImage();
            }
            else
            {
                //Messages
                SetImageText();
            }
        }

        if (dialoguePage == (Mathf.Max(dialogueImages.Length, dialogueMessages.Length) - 1))
        {
            nextButton.SetActive(false);
            if (PodiumMessage)
            {
                acceptButton.SetActive(true);
            }
        }
    }

    #endregion

    #region Photon Methods

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("Lobby"); // load the lobby scene
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // If the player leave the room, do room cleanup stuff (e.x. if they left the room with a loaned orb from the lobby, return that orb)
        if ((TypeOfLevel == LevelTypes.Lobby || TypeOfLevel == LevelTypes.Narrative) && PhotonNetwork.IsMasterClient)
        {
            // decrement num of players ready if player that left had readied up
            bool isPlayerReadiedUp = (bool)otherPlayer.CustomProperties[IsPlayerReady];
            if (isPlayerReadiedUp)
            {
                RPCUnready();
            }
        }

        if (TypeOfLevel == LevelTypes.Lobby && PhotonNetwork.IsMasterClient)
        {
            // why would a bozo claim an orb and then leave, you're making me do this stupid edge case
            string[] orbOwnedKeys = new string[2] { OrbOwnedInLobbyKey1, OrbOwnedInLobbyKey2 };

            PhotonHashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;

            foreach (string orbKey in orbOwnedKeys)
            {
                OrbPodium.OrbTypes orbOwned = (OrbPodium.OrbTypes)otherPlayer.CustomProperties[orbKey];
                if (orbOwned != OrbPodium.OrbTypes.None)
                {
                    // we must return this back to the source  
                    string key = OrbPodium.FetchOrbKey(orbOwned);
                    roomProperties[key] = new int[] { -1, -1 }; // nobody should own the orb anymore
                }
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
            PhotonHashtable emptyProperties = new PhotonHashtable();
            otherPlayer.SetCustomProperties(emptyProperties); // clear the character's properties
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (m_levelType == LevelTypes.Level)
        {
            if (stream.IsWriting)
            {
                //print("sending | num enemies: " + m_enemManager.CurrentNumberEnemiesInLevel); // + " | paint prog:" + m_paintProgress);
                if (m_enemManager)
                {
                    // Synchronize the number of enemies in a level
                    stream.SendNext(m_enemManager.CurrentNumberEnemiesInLevel);
                }
                if (m_paintManager)
                {
                    // Synchronize the paint percentage in a level
                    stream.SendNext(m_paintProgress);
                }
            }
            else
            {
                //string recieveString = "";
                if (m_enemManager)
                {
                    byte test = (byte)stream.ReceiveNext();
                    //recieveString += "num enemies on field: " + test;
                    m_enemManager.SetNumEnemiesOnField(test);
                }
                if (m_paintManager)
                {
                    // master client won't need to recieve paintProgress (they are the authority for that variable)
                    float test2 = (float)stream.ReceiveNext();
                    //recieveString += "paint progress: " + test2;
                    m_paintProgress = test2;
                }
                //print(recieveString);
            }
        }
        else if (m_levelType == LevelTypes.Lobby || m_levelType == LevelTypes.Narrative)
        {
            // Synchronize the number of players ready across all clients, and number of orbs needed
            if (stream.IsWriting)
            {
                stream.SendNext(m_playersReady);
            }
            else
            {
                m_playersReady = (int)stream.ReceiveNext();
            }
        }
    }

    #endregion
}