using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("SCRIPT REFERENCES")]

    // Animator component
    public Animator animator;

    [SerializeField] private Enemy parent;

    [Space(10)]
    [Header("STATS")]

    // Enemy current health
    public float currentHealth;

    // Enemy maximum health
    public float maxHealth;

    [Tooltip("This enemy's knockback resistance. (0-1, 1 being immune)")]
    [SerializeField] private float knockbackResistance;

    [HideInInspector] public bool takingFireDamage;

    public bool immuneToFire;

    [HideInInspector] public bool wasPierceHit;

    private float pierceTimer;
    [SerializeField] private float pierceTime = 0.75f;

    public float Health {
        set {
            currentHealth = value;
            if(currentHealth <= 0) {
                StartDeath();
            }
        }

        get {
            return currentHealth;
        }
    }

    void Start() {

        currentHealth = maxHealth;

        if (animator == null) {
            Debug.Log("EnemyHealth animator is null! Reassigned.");
            animator = this.gameObject.GetComponent<Animator>();
        }
    }

    private void Update() {

        if (wasPierceHit) {
            pierceTimer += Time.deltaTime;

            if (pierceTimer > pierceTime) {
                pierceTimer = 0;
                wasPierceHit = false;
            }
        }
    }

    public void HurtCheck() {
        //wasPierceHit = false;
        animator.SetBool("Hurt", false);
    }

    public virtual void HurtSound() {
        AudioManager.instance.PlaySoundByName("enemy_hurt", transform);
    }

    public void TakeDamage(float damage, Vector2 direction, float knockback) {
        Health -= damage * PlayerController.DamageModifier;
        Debug.Log("Took this amount of damage: " + damage);

        // Play hurt sound
        HurtSound();

        animator.SetBool("Hurt", true);
        parent.kbEd = true;
        if (Health <= 0) {
            StopAllCoroutines();
            StartCoroutine(EnemyDeathKnockback(parent.rb, direction.normalized, knockback));
        } else {
            StopAllCoroutines();
            StartCoroutine(EnemyKnockback(parent.rb, direction.normalized, knockback));
        }
    }

    public void TakeFireDamage(float damage, Vector2 direction, float knockback) {
        Health -= damage * PlayerController.DamageModifier;
        Debug.Log("Took this amount of damage: " + damage);

        // Play hurt sound
        HurtSound();

        animator.SetBool("Hurt", true);
        parent.kbEd = true;
        if (Health <= 0) {
            StopAllCoroutines();
            StartCoroutine(EnemyDeathKnockback(parent.rb, direction.normalized, knockback));
        } else {
            StopAllCoroutines();
            StartCoroutine(EnemyKnockback(parent.rb, direction.normalized, knockback));
        }
        animator.SetBool("FireHurt", true);
        StartCoroutine(FireDamage(PlayerController.BaseFireDamage));
    }

    public IEnumerator FireDamage(float damage) {
        takingFireDamage = true;
        float sum = damage * PlayerController.BaseFireDamage;

        Health -= sum;
        //Debug.Log("Took this amount of FIRE damage: " + damage);

        // Play hurt sound
        HurtSound();

        yield return new WaitForSeconds(1f);

        Health -= sum;
        //Debug.Log("Took this amount of FIRE damage: " + damage);

        // Play hurt sound
        HurtSound();

        yield return new WaitForSeconds(1f);

        Health -= sum;
        //Debug.Log("Took this amount of FIRE damage: " + damage);

        // Play hurt sound
        HurtSound();

        takingFireDamage = false;
    }

    public void FireDamageAnim() {
        if (takingFireDamage) {
            animator.SetBool("FireHurt", true);
        }
        else {
            animator.SetBool("FireHurt", false);
        }
    }

    IEnumerator EnemyKnockback(Rigidbody2D rigidBody, Vector2 dir, float kb) {
        parent.kbEd = true;
        rigidBody.linearVelocity = Vector2.zero;
        rigidBody.AddForce(dir * (kb * (1 - knockbackResistance)), ForceMode2D.Impulse);

        yield return new WaitForSeconds(0.15f);

        rigidBody.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(0.35f);

        parent.kbEd = false;
    }

    IEnumerator EnemyDeathKnockback(Rigidbody2D rigidBody, Vector2 dir, float kb) {
        parent.kbEd = true;
        rigidBody.linearVelocity = Vector2.zero;
        rigidBody.AddForce(dir * (kb * (1 - knockbackResistance) * 4), ForceMode2D.Impulse);

        yield return new WaitForSeconds(0.5f);

        rigidBody.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(0.35f);

        parent.kbEd = false;
    }

    public void StartDeath() {
        animator.SetTrigger("Death");
    }
}
