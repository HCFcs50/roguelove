using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletPiercingScript : BulletScript
{

    public override void Update()
    {
        //Debug.Log("Previous Position: " + previousPosition + "Current Position: " + rb.position);
        Debug.DrawLine(rb.position, previousPosition, Color.yellow, 0.1f);
        //rb.AddForce(direction.normalized * force * Time.fixedDeltaTime);
        //transform.Translate(0, error.y + force * Time.deltaTime, 0);
        //rb.MovePosition(transform.position + force * Time.deltaTime * direction);

        // Raycasts for any missed collisions between frames
        RaycastHit2D[] hits = Physics2D.RaycastAll(previousPosition, ((Vector3)rb.position - previousPosition).normalized, ((Vector3)rb.position - previousPosition).magnitude);

        // For all objects detected in the raycast—
        for (int i = 0; i < hits.Length; i++) {

            Debug.Log(hits[i].collider.gameObject.layer + " " + hits[i].collider.transform.root.name);

            // If the piercing bullet hits an enemy, don't destroy the bullet
            if (hits[i].collider.gameObject.layer == 9) {

                if (hits[i].collider.gameObject.TryGetComponent<EnemyHealth>(out var healthScript)) {

                    if (!healthScript.wasPierceHit) {
                        healthScript.wasPierceHit = true;
                        RegisterPierceDamage(hits[i].collider.gameObject, healthScript);
                    }

                } else {
                    Debug.LogError("Could not get collided enemy's EnemyHealth component!");
                }
                
            }
            // If the object hit isn't supposed to be ignored, then try to deal damage and then destroy the bullet
            else if (!ignoredCollisions.ignoredCollisions.Contains(hits[i].collider.gameObject.layer) && hits[i].collider.gameObject.layer != 9) {
                if (!madeContact) {
                    madeContact = true;
                    RegisterDamage(hits[i].collider.gameObject);
                    coll.enabled = false;
                    rb.linearVelocity = (Vector2)direction.normalized * 0;
                    animator.SetTrigger("Destroy");
                }
            }
            
        }
    }

    public override void OnTriggerEnter2D(Collider2D other)
    {
        if (!madeContact) {

            // If the piercing bullet hits an enemy, don't destroy the bullet
            if (other.gameObject.layer == 9) {

                if (other.gameObject.TryGetComponent<EnemyHealth>(out var healthScript)) {

                    if (!healthScript.wasPierceHit) {
                        healthScript.wasPierceHit = true;
                        RegisterPierceDamage(other.gameObject, healthScript);
                    }

                } else {
                    Debug.LogError("Could not get collided enemy's EnemyHealth component!");
                }
            }
            // If the object hit isn't supposed to be ignored, then try to deal damage and then destroy the bullet
            else if (!ignoredCollisions.ignoredCollisions.Contains(other.gameObject.layer) && other.gameObject.layer != 9) {
                if (!madeContact) {
                    madeContact = true;
                    RegisterDamage(other.gameObject);
                    Debug.Log("BRUHHHHH");
                    coll.enabled = false;
                    rb.linearVelocity = (Vector2)direction.normalized * 0;
                    animator.SetTrigger("Destroy");
                }
            }
        }
    }

    public void RegisterPierceDamage(GameObject target, EnemyHealth enemy) {

        //direction = mousePos - transform.position;

        // Checks whether collided object is an enemy
        if (target.CompareTag("Enemy")) {

            GameStateManager.EOnBulletHitEnemy?.Invoke(); // Triggers bullet hit enemy event

            // If bullet is a flame bullet, deal fire damage
            if (isFire && !enemy.immuneToFire) {
                enemy.TakeFireDamage(damage, direction, knockback);
            }
            else {
                enemy.TakeDamage(damage, direction, knockback);
            }
            
        } else {
            GameStateManager.EOnBulletHitWall?.Invoke(); // Triggers bullet hit wall (not enemy) event
        }
    }
}
