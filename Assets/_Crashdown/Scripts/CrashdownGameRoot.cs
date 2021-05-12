using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CrashdownGameRoot : MonoBehaviour
{
    private const string _mouseAndKeyboardScheme = "Keyboard&Mouse";

    public Projectile projectilePrefab;
    public SoundEffectData sound_UiFailToCrashdown;
    public PlayerInput playerInput;

    public HealthbarFill playerHealthBar;
    public GameObject gameOverScreen;
    public int gameOverSceneIndex = 3;
    public float gameOverScreenDuration = 6.0f;
    public GameObject crashdownPromptRoot;
    public TMPro.TextMeshProUGUI crashdownText;
    public Gradient crashdownTextColorGradient;
    public SoundEffectData crashdownStartToFinishSound;
    public SoundEffectData getPowerupSound;
    public SoundEffectData gameGlitchSound;
    public SoundEffectData buttonPressSound;
    public CosmeticEffect crashdownCosmeticEffect;
    public UnityEngine.UI.Image[] currentWeaponSprites;

    public LayerMask terrainLayer;
    public LayerMask actorsLayer;
    public LayerMask interactionsLayer;

    public Vector3 defaultCameraOffset = new Vector3(0.0f, 5.0f, 0.0f);
    public float defaultCameraAcceleration = 5.0f;

    public PostProcess glitchRenderer;
    public Material[] glitchRendererStages;
    public float glitchRendererTimeBetweenStages = 0.7f;
    public float buttonInteractCoolDown = .65f;

    public bool debugInput = false;
    public bool debugPhysics = false;
    public bool debugCombat = false;
    public bool debugAi = false;

    public static int TotalBossesKilled { get; set; }
    public static int TotalEnemiesKilled { get; set; }
    public static string FinalWeaponUsed { get; set; }
    public static uint TotalFrameCount { get; set; }
    public static float TotalTimeUsed { get; set; }
    public static List<SecretAreaTrigger> SecretAreasFound = new List<SecretAreaTrigger>();

    private Controls _controls;
    private Vector3 _currentCameraVelocity = Vector3.zero;
    private float _gameOverTimer = 0.0f;
    private float _currentCrashdownPromptFlash = 0.0f;
    private static uint currentProjectileCounter = 0;
    private static RaycastHit[] cachedRaycastHitArray = new RaycastHit[32];
    private static Collider[] cachedColliderHitArray = new Collider[8];
    public static Dictionary<Collider, IGameActor> actorColliders = new Dictionary<Collider, IGameActor>();

    public static List<GameObject> DisposeOnLevelChange = new List<GameObject>();

    private string _currentControlScheme;
    private InputAction _aimAction;
    private CrashdownLevelParent currentCrashdownLevel = null;

    private int glitchRendererCurrentStage = 0;
    private float glitchRendererCurrentTime = 0.0f;
    private int nextSceneIndexToLoad = -1;

    private void OnEnable()
    {
        if (_controls == null)
        {
            _controls = new Controls();
        }

        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
        glitchRenderer.enabled = false;

        crashdownPromptRoot.gameObject.SetActive(false);

        _controls.Player.Move.performed += OnMovementChanged;
        _controls.Player.Move.canceled += OnMovementChanged;
        _controls.Player.Move.Enable();
        _controls.Player.Attack.performed += OnAttackDown;
        _controls.Player.Attack.Enable();
        _controls.Player.Dodge.performed += OnDodgeDown;
        _controls.Player.Dodge.Enable();
        _controls.Player.Crashdown.performed += OnCrashdownDown;
        _controls.Player.Crashdown.Enable();
        _controls.Player.Interact.performed += OnInteractDown;
        _controls.Player.Interact.Enable();

        _currentControlScheme = playerInput.currentControlScheme;
        _aimAction = playerInput.actions["Aim"];

        TotalBossesKilled = 0;
        TotalEnemiesKilled = 0;
        FinalWeaponUsed = null;
        TotalTimeUsed = 0.0f;
        TotalFrameCount = 0;
        SecretAreasFound.Clear();
    }

    private void OnDisable()
    {
        _controls.Player.Move.performed -= OnMovementChanged;
        _controls.Player.Move.canceled -= OnMovementChanged;
        _controls.Player.Move.Disable();
        _controls.Player.Attack.performed -= OnAttackDown;
        _controls.Player.Attack.Disable();
        _controls.Player.Dodge.performed -= OnDodgeDown;
        _controls.Player.Dodge.Disable();
        _controls.Player.Crashdown.performed -= OnCrashdownDown;
        _controls.Player.Crashdown.Disable();
        _controls.Player.Interact.performed -= OnInteractDown;
        _controls.Player.Interact.Disable();
    }

    public void OnControlsChanged()
    {
        _currentControlScheme = playerInput.currentControlScheme;
    }

    private void OnMovementChanged(InputAction.CallbackContext context)
    {
        if (debugInput)
        {
            Debug.Log("OnMovementChanged " + context.ToString());
        }
        if (context.performed)
        {
            CrashdownPlayerController.activePlayerInstances[0].InputMovementThisFrame = context.ReadValue<Vector2>();
        }
        if (context.canceled)
        {
            CrashdownPlayerController.activePlayerInstances[0].InputMovementThisFrame = Vector2.zero;
        }
    }

    private void OnAttackDown(InputAction.CallbackContext context)
    {
        if (debugInput)
        {
            Debug.Log("OnAttackDown " + context.ToString());
        }
        CrashdownPlayerController.activePlayerInstances[0].InputAttackDownThisFrame = true;
    }

    private void OnDodgeDown(InputAction.CallbackContext context)
    {
        if (debugInput)
        {
            Debug.Log("OnDodgeDown " + context.ToString());
        }
        CrashdownPlayerController.activePlayerInstances[0].InputDodgeDownThisFrame = true;
    }

    private void OnCrashdownDown(InputAction.CallbackContext context)
    {
        if (debugInput)
        {
            Debug.Log("OnCrashdownDown " + context.ToString());
        }
        CrashdownPlayerController.activePlayerInstances[0].InputCrashdownDownThisFrame = true;
    }

    private void OnInteractDown(InputAction.CallbackContext context)
    {
        if (debugInput)
        {
            Debug.Log("OnInteractDown " + context.ToString());
        }
        CrashdownPlayerController.activePlayerInstances[0].InputInteractDownThisFrame = true;
    }

    void Update()
    {
        if (nextSceneIndexToLoad == -1)
        {
            UpdatePlayers();
            UpdateEnemies();
            UpdateGameLogic();
        }
        else
        {
            // Play a glitch effect for a bit before loading the next level.
            glitchRenderer.enabled = true;
            if (glitchRendererCurrentStage < glitchRendererStages.Length)
            {
                glitchRendererCurrentTime += Time.deltaTime;
                if (glitchRendererCurrentTime > glitchRendererTimeBetweenStages)
                {
                    glitchRendererCurrentTime = 0.0f;
                    glitchRendererCurrentStage++;
                    if (glitchRendererCurrentStage < glitchRendererStages.Length)
                    {
                        glitchRenderer.material = glitchRendererStages[glitchRendererCurrentStage];
                    }
                }
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndexToLoad);
            }
        }
    }

    private void LateUpdate()
    {
        ClearFlags();
    }

    private void UpdatePlayers()
    {
        Vector3 cameraAveragedTargetPosition = Vector3.zero;
        int numberOfCameraTargets = 0;

        // Flatten out inputs relative to the camera.
        Vector3 inputRight = Camera.main.transform.right;
        inputRight.y = 0.0f;
        inputRight = inputRight.normalized;
        Vector3 inputUp = Camera.main.transform.up;
        inputUp.y = 0.0f;
        inputUp = inputUp.normalized;

        bool allPlayersDead = true;
        foreach (CrashdownPlayerController player in CrashdownPlayerController.activePlayerInstances)
        {
            if (!player.IsDead())
            {
                allPlayersDead = false;

                bool debugPlayerIsWalkingAround = true;
                if (debugPlayerIsWalkingAround)
                {
                    Vector3 playerMovementThisFrame = Vector3.zero;

                    bool isDodging = player.RemainingDodgeTime > 0.0f;
                    if (player.CrashdownTarget.HasValue)
                    {
                        // Don't take any input or do any lateral movement.
                    }
                    else if (!isDodging)
                    {
                        Vector2 input = player.InputMovementThisFrame;
                        // Don't make diagonal walking any faster.
                        if (input.sqrMagnitude > 1.0f)
                        {
                            input = input.normalized;
                        }

                        Vector3 worldspaceInput = inputRight * input.x + inputUp * input.y;
                        if (worldspaceInput != Vector3.zero)
                        {
                            player.CurrentFacing = worldspaceInput.normalized;
                        }

                        playerMovementThisFrame = worldspaceInput * player.GetMaxSpeed() * Time.deltaTime;
                    }
                    else
                    {
                        playerMovementThisFrame = player.CurrentFacing * player.GetDodgeSpeed() * Time.deltaTime;
                    }
                    float previousDodgeTime = player.RemainingDodgeTime;
                    player.RemainingDodgeTime -= Time.deltaTime;
                    if (previousDodgeTime >= -player.playerDodgeRefreshDuration && player.RemainingDodgeTime < -player.playerDodgeRefreshDuration)
                    {
                        player.RemainingNumberOfDodges = player.MaximumNumberOfDodges;
                        if (player.RemainingNumberOfDodges > 0)
                        {
                            CosmeticEffect.Spawn(player.playerDodgeRefreshEffect, player.playerDodgeRefreshEffect.defaultLifetime,
                                player.transform.position, Quaternion.identity, player.transform);
                        }
                    }

                    // Move on the X and Z axes separately so the player can slide along walls.
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 newPosition;
                        switch (i)
                        {
                            case 0:
                                newPosition = player.transform.position + new Vector3(playerMovementThisFrame.x, 0.0f, 0.0f);
                                break;
                            default:
                                newPosition = player.transform.position + new Vector3(0.0f, 0.0f, playerMovementThisFrame.z);
                                break;
                        }
                        if (playerMovementThisFrame.sqrMagnitude > 0.0f)
                        {
                            bool targetPositionIsOccupied = false;
                            int numberOfThingsInFrontOfMe = Physics.OverlapSphereNonAlloc(newPosition, player.height / 2.0f, cachedColliderHitArray, actorsLayer.value);
                            if (numberOfThingsInFrontOfMe > 0)
                            {
                                for (int q = 0; q < numberOfThingsInFrontOfMe; q++)
                                {
                                    Collider possibleBlocker = cachedColliderHitArray[q];
                                    if (actorColliders.TryGetValue(possibleBlocker, out IGameActor blockerActor))
                                    {
                                        if (blockerActor is CrashdownEnemyActor
                                            && (blockerActor as CrashdownEnemyActor).aiType == CrashdownEnemyActor.EAiType.InanimateObject)
                                        {
                                            targetPositionIsOccupied = true;
                                            if (debugPhysics)
                                            {
                                                Debug.Log("Player " + player.gameObject.name + " tried to walk into " + (blockerActor as CrashdownEnemyActor).gameObject.name, (blockerActor as CrashdownEnemyActor).gameObject);
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                            bool targetPositionIsOverFloor = Physics.Raycast(newPosition, Vector3.down, out RaycastHit floorHit, player.height * 2.0f, terrainLayer.value);

                            if (!targetPositionIsOccupied)
                            {
                                if (targetPositionIsOverFloor)
                                {
                                    newPosition = floorHit.point + Vector3.up * (player.height / 2.0f);
                                    player.transform.position = newPosition;
                                    if (debugPhysics)
                                    {
                                        Debug.Log("Player " + player.gameObject.name + " is walking on " + floorHit.collider.gameObject.name + " and moved to " + newPosition, floorHit.collider.gameObject);
                                    }
                                }
                                else
                                {
                                    // Player tried to walk off an edge, so they should stop and not move there.
                                    if (debugPhysics)
                                    {
                                        Debug.Log("Player " + player.gameObject.name + " tried to walk off an edge.", player.gameObject);
                                    }
                                }
                            }
                        }
                    }

                    if (_currentControlScheme == _mouseAndKeyboardScheme)
                    {
                        Vector2 mousePosition = _aimAction.ReadValue<Vector2>();

                        Plane plane = new Plane(Vector3.up, player.transform.position);

                        float distance;
                        Ray ray = Camera.main.ScreenPointToRay(mousePosition);

                        if (plane.Raycast(ray, out distance))
                        {
                            var point = ray.GetPoint(distance);
                            var vector = point - player.transform.position;
                            player.CurrentAiming = vector.normalized;
                        }
                        else
                        {
                            player.CurrentAiming = player.CurrentFacing;
                        }
                    }
                    else
                    {
                        player.CurrentAiming = player.CurrentFacing;
                    }

                    player.UpdateFacingAndRenderer();

                    // Player Attacks
                    if (player.RemainingWeaponCooldown <= 0.0f && player.InputAttackDownThisFrame && player.TryGetCurrentWeapon(out WeaponDefinition weapon))
                    {
                        // TODO Cooldowns and so on.
                        ActorUsesWeapon(player, weapon, projectilePrefab);
                        player.RemainingWeaponCooldown = weapon.cooldown;
                    }
                    else
                    {
                        player.RemainingWeaponCooldown -= Time.deltaTime;
                    }

                    // Player Dodges
                    if (player.InputDodgeDownThisFrame)
                    {
                        bool canDodge = true;
                        if (player.IsDodging()
                            || player.RemainingNumberOfDodges <= 0)
                        {
                            canDodge = false;
                        }

                        if (canDodge)
                        {
                            player.RemainingDodgeTime = player.GetDodgeDuration();
                            player.RemainingNumberOfDodges--;
                            CosmeticEffect.Spawn(player.playerDodgeEffect, player.playerDodgeEffect.defaultLifetime,
                                player.transform.position, player.transform.rotation);
                        }
                    }

                    // Player Crashdown
                    if (player.CrashdownTarget.HasValue)
                    {
                        const float kDefaultCrashdownDuration = 1.5f;
                        if (player.CurrentCrashdownTime <= kDefaultCrashdownDuration)
                        {
                            // Crashdown Update Tick
                            player.CurrentCrashdownTime += Time.deltaTime;
                            const float kCrashdownPhaseOneDuration = 1.0f;
                            const float kPlayerRiseDuringCrashdown = 20.0f;
                            if (player.CurrentCrashdownTime < kCrashdownPhaseOneDuration)
                            {
                                player.transform.position += Vector3.up * Time.deltaTime * kPlayerRiseDuringCrashdown / kCrashdownPhaseOneDuration;
                            }
                            else
                            {
                                float distanceToMoveThisFrame = (CrashdownLevelParent.kExpectedDistanceBetweenFloors + kPlayerRiseDuringCrashdown) / (kDefaultCrashdownDuration - kCrashdownPhaseOneDuration) * Time.deltaTime;
                                player.transform.position = Vector3.MoveTowards(player.transform.position, player.CrashdownTarget.Value, distanceToMoveThisFrame); ;
                            }
                        }
                        else
                        {
                            // Crashdown Exit
                            player.transform.position = player.CrashdownTarget.Value + Vector3.up * player.height / 2.0f;
                            ActorUsesWeapon(player, player.crashdownSmashWeapon, projectilePrefab);
                            // spawn here.
                            CosmeticEffect.Spawn(crashdownCosmeticEffect, 2, player.transform.position, Quaternion.identity);
                            player.CrashdownTarget = null;

                            foreach (GameObject o in DisposeOnLevelChange)
                            {
                                if (o != null)
                                {
                                    o.SetActive(false);
                                }
                            }
                            DisposeOnLevelChange.Clear();

                            float levelCutoff = player.transform.position.y + CrashdownLevelParent.kExpectedDistanceBetweenFloors / 2.0f;
                            while (CrashdownLevelParent.activeCrashdownLevels.Count > 0
                                && CrashdownLevelParent.activeCrashdownLevels.Values[0].transform.position.y > levelCutoff)
                            {
                                CrashdownLevelParent.activeCrashdownLevels.Values[0].Dispose();
                            }
                        }
                    }
                    else if (player.HasCrashdownAttack)
                    {
                        if (player.InputCrashdownDownThisFrame)
                        {
                            if (Physics.Raycast(player.transform.position + Vector3.down * player.height * 2.0f, Vector3.down,
                                out RaycastHit raycastHit, CrashdownLevelParent.kExpectedDistanceBetweenFloors * 1.5f, terrainLayer.value))
                            {
                                // Crashdown Start
                                Vector3 targetPoint = raycastHit.point;
                                player.CrashdownTarget = targetPoint;
                                player.CurrentCrashdownTime = 0.0f;
                                player.HasCrashdownAttack = false;
                                crashdownPromptRoot.SetActive(false);
                                player.CurrentFacing = Vector3.back;
                                AudioManager.instance.PlaySound(crashdownStartToFinishSound, player.transform.position);
                            }
                            else
                            {
                                AudioManager.instance.PlaySound(sound_UiFailToCrashdown, player.transform.position);
                            }
                        }
                        _currentCrashdownPromptFlash = Mathf.Repeat(_currentCrashdownPromptFlash + Time.deltaTime, 1.0f);
                        crashdownText.color = crashdownTextColorGradient.Evaluate(_currentCrashdownPromptFlash);
                        // Force the player to use the ability by slowly draining their health while they have it.
                        player.CurrentHealth -= player.crashdownHealthDrainPerSecond * Time.deltaTime;
                        player.CurrentHealthRegenDelay = 1.0f;
                    }

                    // Player Interactions & Secret Areas
                    int numberOfInteractions = Physics.OverlapSphereNonAlloc(player.transform.position, player.height / 2.0f, cachedColliderHitArray, interactionsLayer.value);
                    if (numberOfInteractions > 0)
                    {
                        // Only handle the first interaction, overlapping could get messy.
                        bool hasFoundAnInteraction = false;
                        for (int interactionIndex = 0; interactionIndex < numberOfInteractions; interactionIndex++)
                        {
                            Collider thisInteractionCollider = cachedColliderHitArray[interactionIndex];
                            if (!hasFoundAnInteraction && PlayerInteraction.activeInteractions.TryGetValue(thisInteractionCollider, out PlayerInteraction thisInteraction))
                            {
                                thisInteraction.OnPlayerStaysThisFrame();
                                hasFoundAnInteraction = true;
                                if (player.InputInteractDownThisFrame)
                                {
                                    switch (thisInteraction.interactionType)
                                    {
                                        case PlayerInteraction.EInteractionType.HealthPowerUp:
                                            player.MaxHealth *= player.playerHealthBoostMultiplier;
                                            player.CurrentHealth = player.MaxHealth;
                                            float playerHealthRatio = player.MaxHealth / player.playerStartingHealth;
                                            AudioManager.instance.PlaySound(getPowerupSound, player.transform.position);
                                            break;
                                        case PlayerInteraction.EInteractionType.WeaponPickup:
                                            player.SetCurrentWeapon(thisInteraction.weaponDefinition);
                                            FinalWeaponUsed = thisInteraction.weaponDefinition?.hudAndHighScoreName;
                                            currentWeaponSprites[0].sprite = thisInteraction.weaponDefinition.pickupAndHudSprite;
                                            currentWeaponSprites[0].color = Color.white;
                                            AudioManager.instance.PlaySound(getPowerupSound, player.transform.position);
                                            break;
                                        case PlayerInteraction.EInteractionType.DodgePowerUp:
                                            player.MaximumNumberOfDodges++;
                                            player.RemainingNumberOfDodges = player.MaximumNumberOfDodges;
                                            AudioManager.instance.PlaySound(getPowerupSound, player.transform.position);
                                            break;
                                        case PlayerInteraction.EInteractionType.CrashdownKey:
                                            player.HasCrashdownAttack = true;
                                            player.CurrentHealth = player.MaxHealth; // Fully heal the player so the key can't instakill them.
                                            AudioManager.instance.PlaySound(getPowerupSound, player.transform.position);
                                            crashdownPromptRoot.gameObject.SetActive(true);
                                            break;
                                        case PlayerInteraction.EInteractionType.WinTheGame:
                                            AudioManager.instance.PlaySound(getPowerupSound, player.transform.position);
                                            AudioManager.instance.PlaySound(gameGlitchSound, player.transform.position);
                                            nextSceneIndexToLoad = thisInteraction.targetSceneIndex;
                                            glitchRenderer.enabled = true;
                                            glitchRenderer.material = glitchRendererStages[0];
                                            break;
                                        case PlayerInteraction.EInteractionType.Nothing:
                                            // This object is not interactable, but it can show a tutorial text message when the player is near it.
                                            break;
                                        case PlayerInteraction.EInteractionType.ToggleSomething:
                                            if (thisInteraction.interactionCoolDown <= 0f)
                                            {
                                                thisInteraction.interactionCoolDown = buttonInteractCoolDown;
                                                AudioManager.instance.PlaySound(buttonPressSound, player.transform.position);
                                                foreach (GameObject thing in thisInteraction.objectsToToggle)
                                                {
                                                    bool toggle = thing.activeInHierarchy;
                                                    thing.SetActive(!toggle);
                                                }
                                            }
                                            break;
                                        default:
                                            Debug.LogError("TODO: " + thisInteraction.interactionType.ToString());
                                            break;
                                    }
                                    if (thisInteraction.removeAfterActivation)
                                    {
                                        GameObject.Destroy(thisInteraction.gameObject);
                                    }
                                    else
                                    {
                                        thisInteraction.interactedWithThisFrame = true;
                                    }
                                }
                            }
                            else if (SecretAreaTrigger.activeSecretAreas.TryGetValue(thisInteractionCollider, out SecretAreaTrigger secretArea))
                            {
                                // But do check for all secret areas, don't want to miss one because you landed on a weapon but skipped it.
                                // Trigger Secret Areas On Enter
                                if (!SecretAreasFound.Contains(secretArea))
                                {
                                    SecretAreasFound.Add(secretArea);
                                }
                            }
                        }
                    }

                    // Player Health Regen

                    if (player.CurrentHealthRegenDelay <= 0.0f)
                    {
                        if (player.CurrentHealth < player.MaxHealth)
                        {
                            float regenThisFrame = player.MaxHealth / player.playerFullRegenWait * Time.deltaTime;
                            if (debugCombat)
                            {
                                Debug.Log("Player is regenerating " + regenThisFrame);
                            }
                            player.CurrentHealth = Mathf.Min(player.MaxHealth, player.CurrentHealth + regenThisFrame);
                        }
                    }
                    else
                    {
                        player.CurrentHealthRegenDelay -= Time.deltaTime;
                        if (debugCombat)
                        {
                            Debug.Log("Player has " + player.CurrentHealthRegenDelay + " seconds until they begin to regenerate.");
                        }
                    }

                }

                cameraAveragedTargetPosition += player.transform.position;
                numberOfCameraTargets++;
            }
            //player.InputAttackDownThisFrame = false;
            //player.InputDodgeDownThisFrame = false;
            //player.InputCrashdownDownThisFrame = false;
            //player.InputInteractDownThisFrame = false;

            float playerHealthAmount = player.CurrentHealth / player.MaxHealth;
            playerHealthBar.SetMaxHealth((int)player.MaxHealth);
            playerHealthBar.SetHealth((int)player.CurrentHealth);

            if (player.HasCrashdownAttack)
            {
                playerHealthBar.SetColor(crashdownText.color = crashdownTextColorGradient.Evaluate(_currentCrashdownPromptFlash));
            }
        }

        // Update the camera after all the players have moved.
        if (numberOfCameraTargets > 0)
        {
            cameraAveragedTargetPosition /= numberOfCameraTargets;
            Vector3 cameraNewPosition = cameraAveragedTargetPosition + defaultCameraOffset;
            cameraNewPosition = Vector3.SmoothDamp(Camera.main.transform.position, cameraNewPosition, ref _currentCameraVelocity, 1.0f / defaultCameraAcceleration);
            Camera.main.transform.position = cameraNewPosition;
            Camera.main.transform.LookAt(cameraAveragedTargetPosition, Vector3.forward);
        }

        // Game Over Handling
        if (allPlayersDead)
        {
            gameOverScreen.SetActive(true);
            _gameOverTimer += Time.deltaTime;
            if (CrashdownPlayerController.activePlayerInstances[0].InputInteractDownThisFrame)
            {
                _gameOverTimer += gameOverScreenDuration / 2.0f;
            }
            const float kCameraSpeed = 3.0f;
            Camera.main.transform.position += Vector3.up * Time.deltaTime * kCameraSpeed;
            if (_gameOverTimer > gameOverScreenDuration)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(gameOverSceneIndex);
            }
        }
    }

    private void UpdateEnemies()
    {
        // TODO Spawners attached to levels.
        for (int i = 0; i < CrashdownEnemyActor.activeEnemies.Count; i++)
        {
            CrashdownEnemyActor currentEnemy = CrashdownEnemyActor.activeEnemies[i];
            bool shouldDespawn = false;
            if (debugAi)
            {
                Debug.Log("Enemy " + currentEnemy.gameObject.name + " is in state " + currentEnemy.CurrentAiState, currentEnemy.gameObject);
            }
            // Despawn enemies on floors above.
            if (currentEnemy.transform.position.y - CrashdownPlayerController.activePlayerInstances[0].transform.position.y > CrashdownLevelParent.kExpectedDistanceBetweenFloors / 2.0f)
            {
                currentEnemy.CurrentAiState = CrashdownEnemyActor.EAiState.IsDead;
            }
            switch (currentEnemy.CurrentAiState)
            {
                case CrashdownEnemyActor.EAiState.JustSpawned:
                    currentEnemy.CurrentAggroTarget = null;
                    currentEnemy.CurrentAiState = CrashdownEnemyActor.EAiState.WalkingAndFighting;
                    currentEnemy.RemainingCooldownTime = UnityEngine.Random.Range(0.0f, currentEnemy.maximumRandomAttackDelay);
                    break;
                case CrashdownEnemyActor.EAiState.WalkingAndFighting:
                    if (currentEnemy.CurrentAggroTarget == null)
                    {
                        // Enemy Idle
                        if (TryGetNearestPlayer(currentEnemy.transform.position, currentEnemy.aggroRadius, out IGameActor actor))
                        {
                            currentEnemy.CurrentAggroTarget = actor;
                            foreach (CrashdownEnemyActor ally in currentEnemy.friendsToNotify)
                            {
                                if (ally != null && ally.CurrentAggroTarget == null)
                                {
                                    ally.CurrentAggroTarget = actor;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Enemy Try To Kill Player
                        Vector3 worldspaceMotorInput = Vector3.zero;
                        Vector3 toTarget = currentEnemy.CurrentAggroTarget.GetPosition() - currentEnemy.transform.position;
                        currentEnemy.CurrentFacing = toTarget.normalized;
                        switch (currentEnemy.aiType)
                        {
                            case CrashdownEnemyActor.EAiType.InanimateObject:
                            case CrashdownEnemyActor.EAiType.Stationary:
                                // Just don't move.
                                break;
                            case CrashdownEnemyActor.EAiType.RunAtTheKnees:
                                worldspaceMotorInput = toTarget.normalized;
                                break;
                            case CrashdownEnemyActor.EAiType.OneTimeEnemySpawner:
                                currentEnemy.CurrentAiState = CrashdownEnemyActor.EAiState.Dying;
                                break;
                            default:
                                Debug.LogError("TODO: " + currentEnemy.aiType);
                                break;
                        }

                        // NOTE: This was copypastaed from the player's movement code.
                        // Move on the X and Z axes separately so they can slide along walls.
                        Vector3 enemyMovementThisFrame = worldspaceMotorInput * currentEnemy.GetMoveSpeed() * Time.deltaTime;
                        if (currentEnemy.RemainingEnrageDuration > 0.0f)
                        {
                            Vector3 staggerDirection = Quaternion.Euler(0.0f, 90.0f, 0.0f) * worldspaceMotorInput;
                            enemyMovementThisFrame += staggerDirection * currentEnemy.CurrentSidewaysStaggerAmount * Time.deltaTime;
                        }
                        for (int h = 0; h < 2; h++)
                        {
                            Vector3 newPosition;
                            switch (h)
                            {
                                case 0:
                                    newPosition = currentEnemy.transform.position + new Vector3(enemyMovementThisFrame.x, 0.0f, 0.0f);
                                    break;
                                default:
                                    newPosition = currentEnemy.transform.position + new Vector3(0.0f, 0.0f, enemyMovementThisFrame.z);
                                    break;
                            }
                            if (enemyMovementThisFrame.sqrMagnitude > 0.0f)
                            {
                                if (currentEnemy.ignoresTerrain)
                                {
                                    currentEnemy.MoveTo(newPosition);
                                }
                                else if (Physics.Raycast(newPosition, Vector3.down, out RaycastHit floorHit, currentEnemy.height * 2.0f, terrainLayer.value))
                                {
                                    newPosition = floorHit.point + Vector3.up * (currentEnemy.height / 2.0f);
                                    currentEnemy.MoveTo(newPosition);
                                    if (debugPhysics)
                                    {
                                        Debug.Log("Enemy " + currentEnemy.gameObject.name + " is walking on " + floorHit.collider.gameObject.name + " and moved to " + newPosition, floorHit.collider.gameObject);
                                    }
                                }
                                else
                                {
                                    // Tried to walk off an edge, so they should stop and not move there.
                                    if (debugPhysics)
                                    {
                                        Debug.Log("Enemy " + currentEnemy.gameObject.name + " tried to walk off an edge.", currentEnemy.gameObject);
                                    }
                                }
                            }

                            currentEnemy.CurrentMoving = enemyMovementThisFrame;
                        }

                        currentEnemy.UpdateFacingAndRenderer();

                        if (currentEnemy.CanAttack() && currentEnemy.TryGetCurrentAttack(out WeaponDefinition attack))
                        {
                            ActorUsesWeapon(currentEnemy, attack, projectilePrefab);
                            currentEnemy.firedThisFrame = true;
                            currentEnemy.RemainingCooldownTime = attack.cooldown + UnityEngine.Random.Range(0.0f, currentEnemy.maximumRandomAttackDelay);
                            currentEnemy.AdvanceToNextAttack();
                        }
                        else
                        {
                            float cooldownToLose = Time.deltaTime;
                            if (currentEnemy.RemainingEnrageDuration > 0.0f)
                            {
                                cooldownToLose *= currentEnemy.enrageWeaponCooldownMultiplier;
                            }
                            currentEnemy.RemainingCooldownTime -= cooldownToLose;
                        }
                        currentEnemy.RemainingEnrageDuration -= Time.deltaTime;
                    }
                    break;
                case CrashdownEnemyActor.EAiState.Dying:
                    currentEnemy.timeDying += Time.deltaTime;
                    if (currentEnemy.timeDying >= currentEnemy.deathTime)
                    {
                        currentEnemy.CurrentAiState = CrashdownEnemyActor.EAiState.IsDead;

                        foreach (GameObject nextSpawn in currentEnemy.toSpawnWhenKoed)
                        {
                            if (nextSpawn != null)
                            {
                                Vector2 offset = UnityEngine.Random.insideUnitCircle * currentEnemy.height;
                                Vector3 spawnPosition = currentEnemy.transform.position + new Vector3(offset.x, 0.0f, offset.y);
                                GameObject babby = Instantiate(nextSpawn, spawnPosition, currentEnemy.transform.rotation);
                                if (babby.TryGetComponent(out CrashdownEnemyActor babbyActor))
                                {
                                    babbyActor.CurrentAggroTarget = currentEnemy.CurrentAggroTarget;
                                }
                            }
                        }
                    }
                    break;
                case CrashdownEnemyActor.EAiState.IsDead:
                    shouldDespawn = true;
                    break;
                default:
                    Debug.LogError("TODO: " + currentEnemy.CurrentAiState);
                    break;
            }

            if (shouldDespawn)
            {
                GameObject.Destroy(currentEnemy.gameObject);
                // This calls OnDisable and alters the enemy list.
                i--;
            }
        }
    }

    private void UpdateGameLogic()
    {
        TotalFrameCount++;
        if (TotalFrameCount > 2)
        {
            // In editor, the first two frames of the scene lag a lot because of initialization. We shouldn't add them because they might be hardware-dependent, which hardly seems fair.
            TotalTimeUsed += Time.deltaTime;
        }

        for (int i = 0; i < Projectile.activeProjectiles.Count; i++)
        {
            Projectile currentProjectile = Projectile.activeProjectiles[i];
            bool shouldDespawn = false;
            if (currentProjectile.IsLifetimeOver())
            {
                shouldDespawn = true;
            }
            else
            {
                float distanceToMoveThisFrame = currentProjectile.GetSpeed() * Time.deltaTime;
                int numberOfHits = Physics.SphereCastNonAlloc(currentProjectile.transform.position, currentProjectile.MyWeaponData.radius, currentProjectile.transform.forward, cachedRaycastHitArray, distanceToMoveThisFrame, actorsLayer.value);
                for (int h = 0; h < numberOfHits; h++)
                {
                    RaycastHit currentHit = cachedRaycastHitArray[h];
                    Collider hitCollider = currentHit.collider;
                    if (actorColliders.TryGetValue(hitCollider, out IGameActor touchedActor))
                    {
                        bool canProjectileHitActor = currentProjectile.CanHitActor(touchedActor);
                        if (canProjectileHitActor)
                        {
                            if (debugCombat)
                            {
                                Debug.Log("Projectile hit on " + currentHit.collider.gameObject.name + " for " + currentProjectile.MyWeaponData.damage, currentHit.collider.gameObject);
                            }
                            touchedActor.TakeDamage(currentProjectile.MyWeaponData.damage, currentProjectile.MyOwner);
                            currentProjectile.AddHitToActor(touchedActor);
                        }
                    }
                    else if (Projectile.reflectingColliders.TryGetValue(hitCollider, out IGameActor reflectingActor)
                        && (reflectingActor == null || currentProjectile.MyOwner == null || (reflectingActor.GetTribe() != currentProjectile.MyOwner.GetTribe())))
                    {
                        if (currentProjectile.MyOwner != null)
                        {
                            currentProjectile.RedirectTowards(currentProjectile.MyOwner.GetPosition());
                        }
                        if (reflectingActor != null)
                        {
                            currentProjectile.MyOwner = reflectingActor;
                        }
                    }
                    else
                    {
                        // Recently destroyed colliders might still take raycasts if we're not using DestroyImmediate, so just ignore them.
                    }
                }

                currentProjectile.transform.position += currentProjectile.transform.forward * distanceToMoveThisFrame;
                currentProjectile.RemainingLifetime -= Time.deltaTime;
            }
            if (shouldDespawn)
            {
                GameObject.Destroy(currentProjectile.gameObject);
                // This calls OnDisable and alters the projectile list.
                i--;
            }
        }

        if (CrashdownLevelParent.activeCrashdownLevels.Count > 0 && CrashdownLevelParent.activeCrashdownLevels.Values[0] != currentCrashdownLevel)
        {
            currentCrashdownLevel = CrashdownLevelParent.activeCrashdownLevels.Values[0];
            float currentTime = MusicManager.instance.musicSource.time;
            MusicManager.instance.musicSource.clip = currentCrashdownLevel.myMusic;
            MusicManager.instance.musicSource.pitch = currentCrashdownLevel.myMusicSpeed;
            MusicManager.instance.musicSource.Play();
            MusicManager.instance.musicSource.time = currentTime;
            MusicManager.instance.CurrentLowpassDefault = currentCrashdownLevel.defaultLowpassLevel;
        }
        if (CrashdownPlayerController.activePlayerInstances[0].IsDead())
        {
            MusicManager.instance.musicSource.pitch = 0.5f;
            MusicManager.instance.SetFilterAmount(1.0f);
        }
        else
        {
            float playerHealth = CrashdownPlayerController.activePlayerInstances[0].CurrentHealth / CrashdownPlayerController.activePlayerInstances[0].MaxHealth;
            MusicManager.instance.SetFilterAmount(1.0f - playerHealth);
        }
    }

    private void ActorUsesWeapon(IGameActor actor, WeaponDefinition weapon, Projectile projectilePrefab)
    {
        if (actor != null && weapon != null && weapon.numberToSpawn > 0)
        {
            Quaternion rotationPerShot = Quaternion.identity;
            Quaternion startRotation = actor.GetRotation();
            if (weapon.numberToSpawn > 1)
            {
                float totalAngle = (weapon.numberToSpawn - 1) * weapon.spreadBetweenShotsDegrees;
                float startAngle = totalAngle / 2.0f;
                startRotation = startRotation * Quaternion.Euler(0.0f, startAngle, 0.0f);
                rotationPerShot = Quaternion.Euler(0.0f, -weapon.spreadBetweenShotsDegrees, 0.0f);
            }
            Quaternion currentRotation = startRotation;
            Vector3 spawnOffset = Vector3.forward * weapon.startDistance;
            Vector3 actorCenter = actor.GetPosition();
            for (int i = 0; i < weapon.numberToSpawn; i++)
            {
                Vector3 spawnLocation = currentRotation * spawnOffset + actorCenter;
                Projectile.Spawn(projectilePrefab, weapon, actor, spawnLocation, currentRotation, currentProjectileCounter);
                if (debugCombat)
                {
                    Debug.Log("Actor fired a projectile at " + spawnLocation + " with angle " + currentRotation.eulerAngles.y);
                }
                currentRotation = currentRotation * rotationPerShot;
                if (!weapon.treatProjectilesAsOneWave)
                {
                    currentProjectileCounter++;
                }
            }
            currentProjectileCounter++;

            if (weapon.actorEffectOnFiring != null)
            {
                Transform transformToFollow = null;
                if (actor is Component)
                {
                    transformToFollow = (actor as Component).transform;
                }
                CosmeticEffect.Spawn(weapon.actorEffectOnFiring, weapon.actorEffectOnFiring.defaultLifetime, actor.GetPosition(), actor.GetRotation(), transformToFollow);
            }
        }
    }

    private bool TryGetNearestPlayer(Vector3 position, float radius, out IGameActor player)
    {
        player = null;
        float bestDistance = float.PositiveInfinity;
        foreach (IGameActor actor in CrashdownPlayerController.activePlayerInstances)
        {
            Vector3 actorPos = actor.GetPosition();

            // do not aggro on player if they are on different floor.
            if (Mathf.Abs(actorPos.y - position.y) > 1)
            {
                player = null;
                return false;
            }

            float distance = Vector3.Distance(position, actor.GetPosition());
            if (distance < radius)
            {
                bestDistance = distance;
                player = actor;
            }
        }
        return player != null;
    }

    private void ClearFlags()
    {
        foreach (CrashdownPlayerController player in CrashdownPlayerController.activePlayerInstances)
        {
            player.InputAttackDownThisFrame = false;
            player.InputDodgeDownThisFrame = false;
            player.InputCrashdownDownThisFrame = false;
            player.InputInteractDownThisFrame = false;
            player.WasDamagedThisFrame = false;
        }

        foreach (CrashdownEnemyActor enemy in CrashdownEnemyActor.activeEnemies)
        {
            enemy.ClearFlags();
        }

        foreach (PlayerInteraction interaction in PlayerInteraction.activeInteractions.Values)
        {
            interaction.ClearFlags();
        }
    }

}
