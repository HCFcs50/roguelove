using System.Collections;
using UnityEngine;

public class WeaponSingleShotFire : MonoBehaviour
{
    [Header("SCRIPT REFERENCES")]

    public Weapon parent;

    [SerializeField] private PlayerController player;

    [SerializeField] private CameraShake shake;

    [Header("STATS")]

    public bool canFire = true;
    protected float timer;

    [SerializeField] private float shakeDuration;
    [SerializeField] private float shakeAmplitude;
    [SerializeField] private float shakeFrequency;

    void Start() {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>();

        if (shake == null) {
            Debug.Log("CameraShake camShake is null! Reassigned.");
            //shake = GameObject.FindGameObjectWithTag("VirtualCamera").GetComponent<CameraShake>();
            shake = player.hurtShake;
        }
    }

    /* void Update() {
        if (GameStateManager.GetState() != GameStateManager.GAMESTATE.GAMEOVER) {
            
            Cooldown();

        }
    } */

    public virtual void FixedUpdate()
    {
        if (GameStateManager.GetState() != GameStateManager.GAMESTATE.GAMEOVER) {

            Cooldown();

            // Firing logic, if not on cooldown and mouse button pressed, fire
            if (Input.GetMouseButton(0) && canFire && parent.currentAmmo > 0) {
                Fire();

                // If weapon doesn't have infinite ammo then use ammo
                if (!parent.infiniteAmmo) {
                    UseAmmo();
                }
            }
        }
    }

    // Weapon fire cooldown
    public virtual void Cooldown() {
        
        if (!canFire) {
            timer += Time.fixedDeltaTime;
            
            if(timer > parent.timeBetweenFiring * player.fireRateModifier) {
                canFire = true;
                timer = 0;
            }
        }
    }

    // Firing logic
    public virtual void Fire() {
        canFire = false;

        // Add player damage modifier
        if (parent.ammo.TryGetComponent<BulletScript>(out var bullet)) {
            bullet.damage *= player.damageModifier;
        }

        // Play firing sound
        if (!string.IsNullOrWhiteSpace(parent.fireSound)) {
            FireSound();
        }

        // Start camera shake
        if (shake == null) {
            Debug.Log("CameraShake camShake is null! Reassigned.");
            //shake = GameObject.FindGameObjectWithTag("VirtualCamera").GetComponent<CameraShake>();
            shake = player.hurtShake;
        }
        StartCoroutine(shake.Shake(shakeDuration, shakeAmplitude, shakeFrequency));
        //camShake.Shake(0.15f, 0.4f);

        // Spawn bullet
        GameObject instantBullet = Instantiate(parent.ammo, parent.spawnPos.transform.position, Quaternion.identity);

        // Play muzzle flash animation
        if (parent.spawnPos.TryGetComponent<Animator>(out var animator)) {
            animator.SetTrigger("MuzzleFlash");
        }

        // Destroy bullet after 2 seconds
        StartCoroutine(BulletDestroy(2, instantBullet));
    }

    public virtual void FireSound() {
        FindFirstObjectByType<AudioManager>().Play(parent.fireSound);
    }

    public virtual void UseAmmo() {
        if (parent.currentAmmo - parent.ammoPerClick < 0) {
            parent.currentAmmo = 0;
        }
        else {
            parent.currentAmmo -= parent.ammoPerClick;
        }
    }

    // Destroy bullet if it doesn't hit an obstacle and keeps traveling after some time
    public virtual IEnumerator BulletDestroy(float waitTime, GameObject obj) {
        while (true) {
            yield return new WaitForSeconds(waitTime);
            DestroyImmediate(obj, true);
        }
    }
}
