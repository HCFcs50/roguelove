using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public class PlayerController : MonoBehaviour
{

    [Header("SCRIPT REFERENCES")]

    public LootList lootList;

    [SerializeField] private WeaponPair defaultWeapon;

    [SerializeField] private ContactFilter2D movementFilter;

    public Weapon weapon;

    public GameObject weaponPivot;

    public CapsuleCollider2D contactColl;

    public Rigidbody2D rb;

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private SpriteRenderer spriteRenderer;

    [Tooltip("A reference to the player's health meter.")]
    public HealthBar healthBar;

    [Tooltip("A reference to the player's battery meter.")]
    public EnergyBar energyBar;

    [Tooltip("A reference to the player's coins.")]
    public CoinsUI coinsUI;

    [Tooltip("A reference to the player's ammo meter.")]
    public WeaponInfo ammoBar;

    readonly List<RaycastHit2D> castCollisions = new();
    
    private VolumeProfile volumeProfile;

    [SerializeField] private CircleCollider2D dashColl;

    public CameraShake hurtShake;

    [Header("STATS")]

    public List<GameObject> heldWeapons = new();

    private static int currentWeaponIndex;
    public static int CurrentWeaponIndex { get => currentWeaponIndex; set => currentWeaponIndex = value; }

    private static WeaponRarity primaryWeaponRarity;
    public static WeaponRarity PrimaryWeaponRarity { get => primaryWeaponRarity; set => primaryWeaponRarity = value; }

    private static float primaryWeaponCurrentAmmo;
    public static float PrimaryWeaponCurrentAmmo { get => primaryWeaponCurrentAmmo; set => primaryWeaponCurrentAmmo = value; }

    private static WeaponRarity secondaryWeaponRarity;
    public static WeaponRarity SecondaryWeaponRarity { get => secondaryWeaponRarity; set => secondaryWeaponRarity = value; }

    private static float secondaryWeaponCurrentAmmo;
    public static float SecondaryWeaponCurrentAmmo { get => secondaryWeaponCurrentAmmo; set => secondaryWeaponCurrentAmmo = value; }

    private static int primaryWeaponID;
    public static int PrimaryWeaponID { get => primaryWeaponID; set => primaryWeaponID = value; }

    private static int secondaryWeaponID;
    public static int SecondaryWeaponID { get => secondaryWeaponID; set => secondaryWeaponID = value; }

    [Tooltip("A multiplier that increases or decreases the maximum ammo capacity of the player's weapons.")]
    private static float ammoMaxMultiplier;
    public static float AmmoMaxMultiplier { get => ammoMaxMultiplier; set => ammoMaxMultiplier = value; }

    [SerializeField] private bool canSwitchWeapons = true;

    public bool iFrame;
    
    private Vector2 movementInput;

    // Current direction vector
    [SerializeField] private Vector2 currentDirection;

    // Dash duration boolean
    [SerializeField] private bool isDashing = false;

    // Dash cooldown boolean
    [SerializeField] private bool canDash = true;

    // Dash duration
    [SerializeField] private float dashingTime;
    private float dashTimer = 0;

    // Dash cooldown time
    [SerializeField] private float dashingCooldown;
    private float dashcdTimer = 0;

    // Dash force / power
    [SerializeField] private float dashingPower;

    // Player max health
    private static int maxHealth;
    public static int MaxHealth { get => maxHealth; set => maxHealth = value; }

    public static void AddMaxHealth(int num) {
        MaxHealth += num;
    }

    // Player current health
    public static int currentHealth;

    // Player movement speed
    private static float moveSpeed = 1.0f;
    public static float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
    public static void ChangeMoveSpeed(float speed) {
        MoveSpeed += speed;
    }

    // Player max energy
    private static float maxEnergy;
    public static float MaxEnergy { get => maxEnergy; set => maxEnergy = value; }

    private static bool chargedBattery = false;

    // Player experience / energy
    private static float experience;
    public static float Experience { 
        get => experience; 

        set {
            experience = value;

            if (experience >= maxEnergy) {

                if (!chargedBattery) {
                    chargedBattery = true;

                    // Play battery full animation here!!
                    Debug.Log("Battery full!");

                    GameStateManager.dialogueManager.AddRandomDialogueOfType(DialogueType.NOREQ);

                    // Experience carries over to the next level (still need to make exponentially higher max energy reqs)
                    experience -= maxEnergy;
                }
                
            } else {
                chargedBattery = false;
            }
        }
    }

    public static void AddExperience(float exp) {
        Experience += exp;
    }

    // Player coins
    private static int coins;
    public static int Coins { get => coins; set => coins = value; }

    public static void AddCoins(int value) {
        Coins += value;
    }

    public float damageModifier;

    public float fireRateModifier;

    [SerializeField] private float collisionOffset = 0.01f;

    [SerializeField] private float hurtShakeDuration;
    [SerializeField] private float hurtShakeAmplitude;
    [SerializeField] private float hurtShakeFrequency;

    public bool savePressed = false;

    public int Health {
        set {
            currentHealth = value;
            if(currentHealth <= 0) {
                DeathAnim();
                currentHealth = 0;
                lootList.ResetAllWeapons();
            }
        }

        get {
            return currentHealth;
        }
    }

    // Start is called before the first frame update
    void PlayerStart()
    {

        // Ignores any collisions with enemies (layer 9)
        movementFilter.SetLayerMask(~(1 << 9));
        
        if (rb == null) {
            rb = GetComponent<Rigidbody2D>();
            Debug.Log("PlayerController rb is null! Reassigned.");
        }
        if (animator == null) {
            animator = GetComponentInChildren<Animator>();
            Debug.Log("PlayerController animator is null! Reassigned.");
        }
        if (contactColl == null) {
            contactColl = GetComponentInChildren<CapsuleCollider2D>();
            Debug.Log("Collider2D contactColl is null! Reassigned.");
        }
        if (weapon == null) {
            weapon = GetComponentInChildren<Weapon>();
            Debug.Log("WeaponFireMethod is null! Reassigned.");
        }
        if (volumeProfile == null) {
            volumeProfile = FindAnyObjectByType<Volume>().sharedProfile;
            Debug.Log("VolumeProfile volumeProfile is null! Reassigned.");
        }
        if (hurtShake == null) {
            hurtShake = GameObject.FindGameObjectWithTag("VirtualCamera").GetComponent<CameraShake>();
            Debug.Log("CameraShake camShake is null! Reassigned.");
        }

        // Set health bar and energy bar references on each stage load
        healthBar = GameObject.FindGameObjectWithTag("PlayerHealth").GetComponent<HealthBar>();
        energyBar = GameObject.FindGameObjectWithTag("EnergyBar").GetComponent<EnergyBar>();
        ammoBar = GameObject.FindGameObjectWithTag("AmmoBar").GetComponent<WeaponInfo>();
        coinsUI = GameObject.FindGameObjectWithTag("CoinsUI").GetComponent<CoinsUI>();

        iFrame = false;

        string pathPlayer = Application.persistentDataPath + "/player.franny";

        // Load player info from saved game
        if (File.Exists(pathPlayer) && GameStateManager.SavePressed() == true) {
            LoadPlayer();
        } 
        // Save data exists but player did not click load save --> most likely a NextLevel() call
        else if (File.Exists(pathPlayer) && GameStateManager.SavePressed() == false) {
            Debug.Log("PLAYER SAVE DATA NEXT LEVEL CALL!");
        } 
        // Save data does not exist, and player clicked load save somehow
        else if (!File.Exists(pathPlayer) && GameStateManager.SavePressed() == true) {
            GameStateManager.SetSave(false);
            Debug.LogError("Saved player data not found while trying to load save. How did you get here?");
        } 
        // Save data does not exist and player did not click load save --> most likely started new game
        else if (!File.Exists(pathPlayer) && GameStateManager.SavePressed() == false) {

            // SET DEFAULT STATS
            MaxHealth = 4;
            Health = MaxHealth;
            MaxEnergy = 20;
            Experience = 0;
            MoveSpeed = 3.2f;
            Coins = 0;

            CurrentWeaponIndex = 0;
            PrimaryWeaponID = 1;
            PrimaryWeaponRarity = WeaponRarity.COMMON;
            SecondaryWeaponID = 0;
            SecondaryWeaponRarity = WeaponRarity.COMMON;

            AmmoMaxMultiplier = 1;

            GameObject weaponObject = Instantiate(defaultWeapon.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
            heldWeapons.Add(weaponObject);

            if (heldWeapons[0].TryGetComponent<Weapon>(out var script)) {
                PrimaryWeaponCurrentAmmo = script.ammoMax * AmmoMaxMultiplier;
                script.currentAmmo = PrimaryWeaponCurrentAmmo;
            }
        }

        // Set UI stat objects
        
        healthBar.SetMaxHealth(MaxHealth);
        healthBar.SetHealth(Health);

        energyBar.SetMaxEnergy(MaxEnergy);
        energyBar.SetEnergy(Experience);

        coinsUI.SetCoins(Coins);

        // Add Thornbloom if ID matches and player is not currently holding weapons
        if (PrimaryWeaponID == 1 && heldWeapons.Count == 0) {
            GameObject weaponObject = Instantiate(defaultWeapon.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
            heldWeapons.Add(weaponObject);
        }
        // Add specified ID-matched weapon as primary weapon
        else if (PrimaryWeaponID != 1 && PrimaryWeaponID != 0) {

            Debug.Log(PrimaryWeaponID);
            Debug.Log(PrimaryWeaponRarity);

            WeaponPair pair;
            GameObject weaponObject;
            
            switch (PrimaryWeaponRarity) {

                case WeaponRarity.COMMON:
                    pair = lootList.seenCommonWeapons.Find(x => x.pickupScript.weaponID == PrimaryWeaponID);
                    weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                    heldWeapons.Add(weaponObject);
                    break;
                case WeaponRarity.UNCOMMON:
                    pair = lootList.seenUncommonWeapons.Find(x => x.pickupScript.weaponID == PrimaryWeaponID);
                    weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                    heldWeapons.Add(weaponObject);
                    break;
                case WeaponRarity.RARE:
                    pair = lootList.seenRareWeapons.Find(x => x.pickupScript.weaponID == PrimaryWeaponID);
                    weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                    heldWeapons.Add(weaponObject);
                    break;
                case WeaponRarity.EPIC:
                    pair = lootList.seenEpicWeapons.Find(x => x.pickupScript.weaponID == PrimaryWeaponID);
                    weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                    heldWeapons.Add(weaponObject);
                    break;
                case WeaponRarity.LEGENDARY:
                    pair = lootList.seenLegendaryWeapons.Find(x => x.pickupScript.weaponID == PrimaryWeaponID);
                    weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                    heldWeapons.Add(weaponObject);
                    break;
            }
        }

        // Add saved secondary weapon
        if (SecondaryWeaponID != 0) {

            Debug.Log(SecondaryWeaponID);
            Debug.Log(SecondaryWeaponRarity);

            WeaponPair pair;
            GameObject weaponObject;

            if (SecondaryWeaponID == 1) {
                pair = defaultWeapon;
                weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                heldWeapons.Add(weaponObject);
            } else {
                switch (SecondaryWeaponRarity) {

                    case WeaponRarity.COMMON:
                        pair = lootList.seenCommonWeapons.Find(x => x.pickupScript.weaponID == SecondaryWeaponID);
                        weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                        heldWeapons.Add(weaponObject);
                        break;
                    case WeaponRarity.UNCOMMON:
                        pair = lootList.seenUncommonWeapons.Find(x => x.pickupScript.weaponID == SecondaryWeaponID);
                        weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                        heldWeapons.Add(weaponObject);
                        break;
                    case WeaponRarity.RARE:
                        pair = lootList.seenRareWeapons.Find(x => x.pickupScript.weaponID == SecondaryWeaponID);
                        weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                        heldWeapons.Add(weaponObject);
                        break;
                    case WeaponRarity.EPIC:
                        pair = lootList.seenEpicWeapons.Find(x => x.pickupScript.weaponID == SecondaryWeaponID);
                        weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                        heldWeapons.Add(weaponObject);
                        break;
                    case WeaponRarity.LEGENDARY:
                        pair = lootList.seenLegendaryWeapons.Find(x => x.pickupScript.weaponID == SecondaryWeaponID);
                        weaponObject = Instantiate(pair.pickupScript.weaponObject, weaponPivot.transform.position, Quaternion.identity, weaponPivot.transform);
                        heldWeapons.Add(weaponObject);
                        break;
                }
            }
        }

        // Update weapons with saved ammo amounts
        if (heldWeapons.Count == 2) {
            if (heldWeapons[0].TryGetComponent<Weapon>(out var primary)) {
                primary.currentAmmo = PrimaryWeaponCurrentAmmo;
                primary.ammoMax *= AmmoMaxMultiplier;
                Debug.Log(PrimaryWeaponCurrentAmmo);
            }

            if (heldWeapons[1].TryGetComponent<Weapon>(out var secondary)) {
                secondary.currentAmmo = SecondaryWeaponCurrentAmmo;
                secondary.ammoMax *= AmmoMaxMultiplier;
                Debug.Log(SecondaryWeaponCurrentAmmo);
            }
        } else if (heldWeapons.Count > 0) {
            if (heldWeapons[0].TryGetComponent<Weapon>(out var primary)) {
                primary.currentAmmo = PrimaryWeaponCurrentAmmo;
                primary.ammoMax *= AmmoMaxMultiplier;
            }
        }

        // Set held weapon to previously held weapon upon entering new level
        if (CurrentWeaponIndex == 0 && heldWeapons.Count > 0) {
            if (heldWeapons[0].TryGetComponent<Weapon>(out var weaponScript)) {

                if (heldWeapons.Count > 1) {
                    heldWeapons[1].SetActive(false);
                }

                if (weapon == null) {
                    weapon = weaponScript;
                    CurrentWeaponIndex = 0;
                }

                // Sets maximum weapon ammo UI stat to base value * static multiplier
                ammoBar.SetMaxAmmo(weaponScript.ammoMax * AmmoMaxMultiplier);
                ammoBar.SetAmmo(PrimaryWeaponCurrentAmmo, weaponScript);
                ammoBar.weaponSprite.sprite = weaponScript.sprite.sprite;
            }
        } else if (CurrentWeaponIndex == 1 && heldWeapons.Count > 1) {
            if (heldWeapons[1].TryGetComponent<Weapon>(out var weaponScript)) {

                heldWeapons[0].SetActive(false);

                if (weapon == null) {
                    weapon = weaponScript;
                    CurrentWeaponIndex = 1;
                }

                // Sets maximum weapon ammo to base value * static multiplier
                ammoBar.SetMaxAmmo(weaponScript.ammoMax * AmmoMaxMultiplier);
                ammoBar.SetAmmo(SecondaryWeaponCurrentAmmo, weaponScript);
                ammoBar.weaponSprite.sprite = weaponScript.sprite.sprite;
            }
        }
    }

    private void Update() {

        if (savePressed) {
            savePressed = false;
            PlayerStart();
            SavePlayer();
            GameStateManager.SetSave(false);
        }

        // If in a menu, then do not take any input
        if (GameStateManager.GetState() == GAMESTATE.MENU) {
            return;
        }

        // Dash
        if (Input.GetKeyDown(KeyCode.Space) && canDash) {
            canDash = false;
            isDashing = true;
            animator.SetBool("Dash", true);
        }

        // WEAPON SWITCHING AND DROPPING MECHANICS

        // Switch to first weapon
        if ((Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Alpha1)) && canSwitchWeapons 
        && heldWeapons.Count == 2) {

            if (!heldWeapons[0].activeInHierarchy) {
                StartWeaponSwitch(0, currentWeaponIndex);
            }
        }
        // Switch to second weapon
        else if ((Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Alpha2)) && canSwitchWeapons 
        && heldWeapons.Count == 2) {

            if (!heldWeapons[1].activeInHierarchy) {
                StartWeaponSwitch(1, currentWeaponIndex);
            }
        }

        // Drop selected weapon
        if (Input.GetKeyDown(KeyCode.Q) && weapon != null) {

            // Put currently selected weapon's pickup object into temporary variable
            GameObject dropped = Instantiate(weapon.weaponPickup, transform.position, Quaternion.identity);

            // If script is obtainable, set this weapon to the pickup object's weaponObject (to be parented to player on pickup)
            if (dropped.TryGetComponent<WeaponPickup>(out var script)) {

                // Set weapon pickup to "dropped" setting
                script.dropped = true;

                Debug.Log(CurrentWeaponIndex);

                // Drop a new pickup weapon
                script.weaponObject = heldWeapons[CurrentWeaponIndex];

                // Delete dropped weapon
                if (heldWeapons.Count == 2) {
                    int queuedForDeletion = CurrentWeaponIndex;

                    // Switch to other weapon before deleting
                    StartWeaponSwitch(heldWeapons.Count - 1 - CurrentWeaponIndex, CurrentWeaponIndex);

                    heldWeapons[queuedForDeletion].transform.parent = null;
                    heldWeapons[queuedForDeletion].SetActive(false);

                    heldWeapons.Remove(heldWeapons[queuedForDeletion]);
                } else {
                    int queuedForDeletion = CurrentWeaponIndex;

                    heldWeapons[queuedForDeletion].transform.parent = null;
                    heldWeapons[queuedForDeletion].SetActive(false);

                    heldWeapons.Remove(heldWeapons[queuedForDeletion]);
                    weapon = null;
                }
            } else {
                Debug.LogWarning("Could not find WeaponPickup component on weapon being dropped!");
            }

            switch (heldWeapons.Count) {

                // If no weapons are held, set everything to null and disable ammo UI bar
                case 0:
                    ammoBar.gameObject.SetActive(false);

                    CurrentWeaponIndex = 0;

                    PrimaryWeaponID = 0;
                    PrimaryWeaponRarity = WeaponRarity.COMMON;

                    SecondaryWeaponID = 0;
                    SecondaryWeaponRarity = WeaponRarity.COMMON;
                    break;
                // If one held weapon left after dropping, set primary vars to that weapon, and null secondary vars
                case 1:
                    CurrentWeaponIndex = 0;

                    PrimaryWeaponID = weapon.id;
                    PrimaryWeaponRarity = weapon.rarity;

                    SecondaryWeaponID = 0;
                    SecondaryWeaponRarity = WeaponRarity.COMMON;
                    break;
            }
        }
    }

    public void StartWeaponSwitch(int switchTo, int switchFrom) {
        StartCoroutine(SwitchWeapons(switchTo, switchFrom));
    }

    public IEnumerator SwitchWeapons(int switchTo, int switchFrom) {

        // Prevent switching weapons while already switching weapons
        canSwitchWeapons = false;

        // Disable old weapon if switching between two different weapons
        if (switchTo != switchFrom) {
            heldWeapons[switchFrom].SetActive(false);
        }

        if (heldWeapons[switchTo].TryGetComponent<Weapon>(out var gotWeapon)) {

            // Set weapon script reference to new weapon
            weapon = gotWeapon;

            // Set currently held weapon index number to new weapon
            CurrentWeaponIndex = switchTo;

            // Enable new weapon
            if (!heldWeapons[switchTo].activeInHierarchy) {
                heldWeapons[switchTo].SetActive(true);
            }

            // If ammo UI bar is disabled, enable it
            if (!ammoBar.gameObject.activeInHierarchy) {
                ammoBar.gameObject.SetActive(true);
            }

            ammoBar.SetMaxAmmo(gotWeapon.ammoMax * AmmoMaxMultiplier);

            switch (switchTo) {
                case 0:
                    ammoBar.SetAmmo(PrimaryWeaponCurrentAmmo, gotWeapon);
                    break;
                case 1:
                    ammoBar.SetAmmo(SecondaryWeaponCurrentAmmo, gotWeapon);
                    break;
            }

            ammoBar.weaponSprite.sprite = gotWeapon.sprite.sprite;
        }
        else {
            Debug.LogError("Tried to switch to nonexistent weapon!");
        }

        yield return new WaitForSeconds(0.6f);

        canSwitchWeapons = true;
    }
    
    private void FixedUpdate() {

        // If in a menu, then do not take any input
        if (GameStateManager.GetState() == GAMESTATE.MENU) {
            animator.SetBool("IsMoving", false);
            return;
        }

        // Dash for the remaining duration, and don't take anything else as input
        if (isDashing) {

            // Enable reflection bullet radius
            dashColl.gameObject.SetActive(true);

            // Reset velocity to zero before dashing
            rb.velocity = Vector2.zero;
            TryDash(currentDirection);
            
            // Dash duration timer
            dashTimer += Time.fixedDeltaTime;
                
            if(dashTimer > dashingTime) {

                // Disable reflection bullet radius
                dashColl.gameObject.SetActive(false);

                // End dash
                isDashing = false;

                // End dash animation
                animator.SetBool("Dash", false);

                // Reser velocity to zero after dashing
                rb.velocity = Vector2.zero;

                // Reset dash duration timer
                dashTimer = 0;
            }

            return;
        }

        // Movement system if you're not dead lol
        if (GameStateManager.GetState() != GAMESTATE.GAMEOVER) {

            // Dash cooldown timer
            if (!isDashing && !canDash) {
                dashcdTimer += Time.fixedDeltaTime;

                if(dashcdTimer > dashingCooldown) {

                    // Reset dash cooldown
                    canDash = true;

                    // Reset dash cooldown timer
                    dashcdTimer = 0;
                }
            }

            if (movementInput != Vector2.zero) {
                bool success = TryMove(movementInput);
                currentDirection = movementInput;

                if (!success) {
                    success = TryMove(new Vector2(movementInput.x, 0));
                    currentDirection = new Vector2(movementInput.x, 0);

                    if (!success) {
                        success = TryMove(new Vector2(0, movementInput.y));
                        currentDirection = new Vector2(0, movementInput.y);
                    }
                }

                animator.SetBool("IsMoving", success);
            } else {
                animator.SetBool("IsMoving", false);
            }

            // Set direction of sprite to movement direction
            if(movementInput.x < 0) {
                spriteRenderer.flipX = true;
            } else if (movementInput.x > 0) {
                spriteRenderer.flipX = false;
            }
        }
    }

    // Dash function
    private bool TryDash(Vector2 direction) {

        if (direction != Vector2.zero) {

            int count = rb.Cast(
                direction, 
                movementFilter, 
                castCollisions, 
                MoveSpeed * Time.fixedDeltaTime + collisionOffset);
        
            if(count == 0) {
                rb.AddForce(direction * dashingPower * Time.fixedDeltaTime, ForceMode2D.Impulse);
                return true;
            } else {
                return false;
            }
        } else {
            // can't move if there's no direction to move in
            return false;
        }
    }

    // Move function
    private bool TryMove(Vector2 direction) {
        if (direction != Vector2.zero) {
            int count = rb.Cast(direction, movementFilter, castCollisions, MoveSpeed * Time.fixedDeltaTime + collisionOffset);
        
            if(count == 0) {
                rb.MovePosition(rb.position + direction * MoveSpeed * Time.fixedDeltaTime);
                return true;
            } else {
                return false;
            }
        } else {
            // can't move if there's no direction to move in
            return false;
        }
    }

    void OnMove(InputValue movementValue) {
        movementInput = movementValue.Get<Vector2>();
    }

    void OnFire() {
        /*
        animator.SetTrigger("MeleeAttack");
        */
    }

    public void TakeDamage(int damage) {

        if (GameStateManager.GetState() != GAMESTATE.GAMEOVER && iFrame == false) {
            iFrame = true;
            StartCoroutine(SetHurtFlash(true));
            StartCoroutine(hurtShake.Shake(hurtShakeDuration, hurtShakeAmplitude, hurtShakeFrequency));
            Health -= damage;
            healthBar.SetHealth(currentHealth);
            Debug.Log("Player took damage!");

            animator.SetBool("Hurt", true);
        }
    }

    private IEnumerator SetHurtFlash(bool condition) {

        if (volumeProfile != null) {
            if (volumeProfile.TryGet<ColorAdjustments>(out var colorAdjust)) {
                colorAdjust.active = condition;
                yield return new WaitForSeconds(0.2f);
                colorAdjust.active = !condition;
            }
        }

        yield return null;
    }

    public void SavePlayer () {
        SaveSystem.SavePlayer(this, weapon);
        Debug.Log("SAVE PLAYER CALLED");
    }

    public void LoadPlayer() {
        
        // Load save data
        PlayerData data = SaveSystem.LoadPlayer();

        // Load health
        MaxHealth = data.playerMaxHealth;
        Health = data.playerHealth;
        healthBar.SetMaxHealth(MaxHealth);
        healthBar.SetHealth(Health);

        // Load damage modifier
        damageModifier = data.playerDamageModifier;

        // Load experience level
        MaxEnergy = data.maxExperienceLevel;
        Experience = data.experienceLevel;
        energyBar.SetMaxEnergy(data.maxExperienceLevel);
        energyBar.SetEnergy(data.experienceLevel);

        // Set speeds
        MoveSpeed = data.playerMoveSpeed;
        fireRateModifier = data.playerFireRateModifier;

        // Load coins
        Coins = data.playerCoins;
        coinsUI.SetCoins(data.playerCoins);

        // Load weapons
        PrimaryWeaponID = data.primaryWeaponID;
        PrimaryWeaponRarity = (WeaponRarity)data.primaryWeaponRarity;
        SecondaryWeaponID = data.secondaryWeaponID;
        SecondaryWeaponRarity = (WeaponRarity)data.secondaryWeaponRarity;

        AmmoMaxMultiplier = data.ammoMaxMultiplier;
        PrimaryWeaponCurrentAmmo = data.primaryWeaponCurrentAmmo;
        SecondaryWeaponCurrentAmmo = data.secondaryWeaponCurrentAmmo;

        Debug.Log("LOADED PLAYER");
    }

    public void DeathAnim() {
        animator.SetBool("Death", true);
        FindFirstObjectByType<AudioManager>().Play("PlayerDeath");
    }
}
